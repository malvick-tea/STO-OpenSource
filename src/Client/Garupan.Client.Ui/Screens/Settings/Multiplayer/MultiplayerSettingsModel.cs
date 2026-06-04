using System;
using Garupan.Client.Core.Application;
using Opus.Engine.Input;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Settings.Multiplayer;

/// <summary>
/// Pure data model for <see cref="MultiplayerSettingsScreen"/>: three stacked
/// endpoint sections (default + Hungry Battles override + Tactical 5v5 override), each
/// with a host and a port text field, plus a focused-field index in [0, FieldCount).
/// No service or VFS dependency — the screen owns the bridge to <c>SettingsService</c>
/// via the <see cref="Changed"/> event.
/// </summary>
/// <remarks>
/// <para>
/// Field index → section + slot mapping is deterministic: index 0/1 = default host/port,
/// 2/3 = Hungry, 4/5 = Tactical. Constants are exposed so the renderer + layout + tests
/// reference rows symbolically.
/// </para>
/// <para>
/// Override semantics: a section commits as
/// <see cref="MultiplayerEndpointOverride.None"/> when BOTH its host and port fields are
/// empty — the resolver then falls back to the default endpoint. Any other combination
/// stamps a populated override record; <see cref="MultiplayerEndpointOverride.IsConfigured"/>
/// gates whether the resolver actually uses it (empty host or out-of-range port reads as
/// no override even if the record exists).
/// </para>
/// </remarks>
public sealed class MultiplayerSettingsModel
{
    public const int SectionDefault = 0;
    public const int SectionHungry = 1;
    public const int SectionTactical = 2;
    public const int SectionCount = 3;
    public const int FieldsPerSection = 2;
    public const int FieldCount = SectionCount * FieldsPerSection;

    public const int HostFieldIndex = 0;
    public const int PortFieldIndex = 1;
    public const int HungryHostFieldIndex = 2;
    public const int HungryPortFieldIndex = 3;
    public const int TacticalHostFieldIndex = 4;
    public const int TacticalPortFieldIndex = 5;

    /// <summary>Max digits in a 16-bit port — 65535 is five characters. The text field
    /// caps at this width so typing past the cap silently no-ops.</summary>
    private const int MaxPortDigits = 5;

    private readonly TextInputField[] _fields = new TextInputField[FieldCount];
    private MultiplayerSettings _current;
    private int _selectedField;

    public MultiplayerSettingsModel(MultiplayerSettings initial)
    {
        _current = Ensure.NotNull(initial);
        _fields[HostFieldIndex] = HostField(initial.Host);
        _fields[PortFieldIndex] = PortField(FormatPort(initial.Port));
        _fields[HungryHostFieldIndex] = HostField(initial.HungryBattles.Host);
        _fields[HungryPortFieldIndex] = PortField(FormatOverridePort(initial.HungryBattles.Port));
        _fields[TacticalHostFieldIndex] = HostField(initial.Tactical.Host);
        _fields[TacticalPortFieldIndex] = PortField(FormatOverridePort(initial.Tactical.Port));
    }

    /// <summary>Raised once per coherent commit, carrying the new snapshot. Typing does
    /// not raise this — only Enter, focus change, or screen close.</summary>
    public event Action<MultiplayerSettings>? Changed;

    public MultiplayerSettings Current => _current;

    /// <summary>The default endpoint's host field — kept as a named accessor so existing
    /// callers + tests survive the Phase-51 multi-section refactor untouched.</summary>
    public TextInputField Host => _fields[HostFieldIndex];

    /// <summary>The default endpoint's port field — same Phase-51 backward-compat shim.</summary>
    public TextInputField Port => _fields[PortFieldIndex];

    /// <summary>Direct field accessor for the renderer / mouse hit-test, indexed by the
    /// public constants above. Out-of-range index throws — the layout never asks for
    /// rows outside [0, FieldCount).</summary>
    public TextInputField Field(int index) => _fields[index];

    public int SelectedField => _selectedField;

    /// <summary>True if every section parses to a usable snapshot — the default has a
    /// non-empty host + a port in range, and each override is either fully empty (no
    /// override) or has a valid host + a valid port. Mid-typing empty port fields are
    /// tolerated because commit substitutes the per-section fallback.</summary>
    public bool IsValid
    {
        get
        {
            if (_fields[HostFieldIndex].IsEmpty)
            {
                return false;
            }

            if (!IsValidDefaultPortField(_fields[PortFieldIndex]))
            {
                return false;
            }

            return IsValidOverrideSection(_fields[HungryHostFieldIndex], _fields[HungryPortFieldIndex])
                && IsValidOverrideSection(_fields[TacticalHostFieldIndex], _fields[TacticalPortFieldIndex]);
        }
    }

    /// <summary>Moves the focused field by ±1, clamped. Commits the current field's
    /// edits before moving so the listener sees one coherent snapshot.</summary>
    public void MoveSelection(int direction)
    {
        var step = Math.Sign(direction);
        if (step == 0)
        {
            return;
        }

        Commit();
        _selectedField = Math.Clamp(_selectedField + step, 0, FieldCount - 1);
        _fields[_selectedField] = _fields[_selectedField].WithCursorAtEnd();
    }

    /// <summary>Parks focus on <paramref name="index"/> (e.g. follows the mouse). Out-of-range
    /// values are ignored. Commits any pending edit on the prior field before moving.</summary>
    public void SelectField(int index)
    {
        if (index < 0 || index >= FieldCount || index == _selectedField)
        {
            return;
        }

        Commit();
        _selectedField = index;
    }

    /// <summary>Types <paramref name="ch"/> into the focused field. A character rejected
    /// by the field's filter (or a null char) is silently dropped.</summary>
    public void Type(char ch) =>
        _fields[_selectedField] = _fields[_selectedField].WithTyped(ch);

    public void Backspace() =>
        _fields[_selectedField] = _fields[_selectedField].WithBackspace();

    public void MoveCursor(int direction) =>
        _fields[_selectedField] = _fields[_selectedField].WithCursor(direction);

    /// <summary>Commits all fields to a new <see cref="MultiplayerSettings"/> snapshot
    /// and raises <see cref="Changed"/> only when the snapshot actually moved.</summary>
    public void Commit()
    {
        var next = BuildSnapshot();
        if (next == _current)
        {
            return;
        }

        _current = next;
        Changed?.Invoke(_current);
    }

    /// <summary>Restores the fields from <paramref name="snapshot"/> — used by Esc / Cancel
    /// paths that need to roll back to a known-good state. Never raises <see cref="Changed"/>.</summary>
    public void Reset(MultiplayerSettings snapshot)
    {
        Ensure.NotNull(snapshot);
        _current = snapshot;
        _fields[HostFieldIndex] = HostField(snapshot.Host);
        _fields[PortFieldIndex] = PortField(FormatPort(snapshot.Port));
        _fields[HungryHostFieldIndex] = HostField(snapshot.HungryBattles.Host);
        _fields[HungryPortFieldIndex] = PortField(FormatOverridePort(snapshot.HungryBattles.Port));
        _fields[TacticalHostFieldIndex] = HostField(snapshot.Tactical.Host);
        _fields[TacticalPortFieldIndex] = PortField(FormatOverridePort(snapshot.Tactical.Port));
    }

    private MultiplayerSettings BuildSnapshot() => _current with
    {
        Host = _fields[HostFieldIndex].IsEmpty ? MultiplayerSettings.LoopbackHost : _fields[HostFieldIndex].Value,
        Port = ParseDefaultPort(_fields[PortFieldIndex].Value),
        HungryBattles = BuildOverride(_fields[HungryHostFieldIndex], _fields[HungryPortFieldIndex]),
        Tactical = BuildOverride(_fields[TacticalHostFieldIndex], _fields[TacticalPortFieldIndex]),
    };

    private static MultiplayerEndpointOverride BuildOverride(TextInputField hostField, TextInputField portField)
    {
        if (hostField.IsEmpty && portField.IsEmpty)
        {
            return MultiplayerEndpointOverride.None;
        }

        return new MultiplayerEndpointOverride
        {
            Host = hostField.IsEmpty ? string.Empty : hostField.Value,
            Port = ParseOverridePort(portField.Value),
        };
    }

    private static int ParseDefaultPort(string text)
    {
        if (string.IsNullOrEmpty(text) ||
            !int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return MultiplayerSettings.DefaultPort;
        }

        return Math.Clamp(value, MultiplayerSettings.MinPort, MultiplayerSettings.MaxPort);
    }

    /// <summary>Override-port parsing: an empty field stays 0 (the "no override" sentinel
    /// the resolver reads); any populated value clamps to legal port range.</summary>
    private static int ParseOverridePort(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        if (!int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return 0;
        }

        return Math.Clamp(value, MultiplayerSettings.MinPort, MultiplayerSettings.MaxPort);
    }

    private static bool IsValidDefaultPortField(TextInputField portField)
    {
        if (!int.TryParse(portField.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var port))
        {
            return portField.IsEmpty;
        }

        return port >= MultiplayerSettings.MinPort && port <= MultiplayerSettings.MaxPort;
    }

    private static bool IsValidOverrideSection(TextInputField hostField, TextInputField portField)
    {
        if (hostField.IsEmpty && portField.IsEmpty)
        {
            return true;
        }

        if (!int.TryParse(portField.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var port))
        {
            return portField.IsEmpty;
        }

        return port >= MultiplayerSettings.MinPort && port <= MultiplayerSettings.MaxPort;
    }

    private static TextInputField HostField(string seed) =>
        TextInputField.From(seed ?? string.Empty, MultiplayerSettings.MaxHostLength, TextInputFilters.Host);

    private static TextInputField PortField(string seed) =>
        TextInputField.From(seed, MaxPortDigits, TextInputFilters.Port);

    private static string FormatPort(int port) =>
        port.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Override port → text field seed: a zero port (the "no override" sentinel)
    /// shows as the empty string, so a section without an override displays blank to the
    /// player; any populated value formats normally.</summary>
    private static string FormatOverridePort(int port) =>
        port == 0 ? string.Empty : port.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
