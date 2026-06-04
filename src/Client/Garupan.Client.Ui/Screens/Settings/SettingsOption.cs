using System;
using System.Collections.Generic;
using Garupan.Client.Core.Application;
using Opus.Localisation;

namespace Garupan.Client.Ui.Screens.Settings;

/// <summary>
/// One adjustable row on the settings screen. An option reads its current value from an
/// <see cref="AppSettings"/> snapshot and produces a new snapshot when cycled — it never
/// holds mutable state of its own, so <see cref="SettingsModel"/> stays the single owner
/// of the live settings.
/// </summary>
public abstract class SettingsOption
{
    protected SettingsOption(TranslationKey label, TranslationKey? sectionHeader, bool restartRequired)
    {
        Label = label;
        SectionHeader = sectionHeader;
        RestartRequired = restartRequired;
    }

    /// <summary>Translation key for the row's left-hand label.</summary>
    public TranslationKey Label { get; }

    /// <summary>Set on the first option of a section — the renderer draws a header above it.</summary>
    public TranslationKey? SectionHeader { get; }

    /// <summary>True when changing this value only takes effect after an app restart.</summary>
    public bool RestartRequired { get; }

    /// <summary>The value column text for the given settings snapshot.</summary>
    public abstract string Display(AppSettings settings);

    /// <summary>Returns a snapshot with this option stepped by <paramref name="direction"/>
    /// (-1 / +1). Returns an equal snapshot when the step is a no-op (e.g. clamped).</summary>
    public abstract AppSettings Cycle(AppSettings settings, int direction);
}

/// <summary>One discrete choice of a <see cref="SettingsChoiceOption"/>.</summary>
/// <param name="Display">The value-column text when this choice is selected.</param>
/// <param name="IsCurrent">True when <paramref name="Apply"/>'s effect is already in place.</param>
/// <param name="Apply">Produces a snapshot with this choice selected.</param>
public sealed record SettingsChoice(
    string Display,
    Func<AppSettings, bool> IsCurrent,
    Func<AppSettings, AppSettings> Apply);

/// <summary>An option that cycles through a fixed, wrapping list of discrete choices —
/// locale, vsync, window resolution.</summary>
public sealed class SettingsChoiceOption : SettingsOption
{
    private readonly IReadOnlyList<SettingsChoice> _choices;

    public SettingsChoiceOption(
        TranslationKey label,
        TranslationKey? sectionHeader,
        bool restartRequired,
        IReadOnlyList<SettingsChoice> choices)
        : base(label, sectionHeader, restartRequired)
    {
        _choices = choices;
    }

    public override string Display(AppSettings settings) => _choices[CurrentIndex(settings)].Display;

    public override AppSettings Cycle(AppSettings settings, int direction)
    {
        var count = _choices.Count;
        var next = (((CurrentIndex(settings) + direction) % count) + count) % count;
        return _choices[next].Apply(settings);
    }

    private int CurrentIndex(AppSettings settings)
    {
        for (var i = 0; i < _choices.Count; i++)
        {
            if (_choices[i].IsCurrent(settings))
            {
                return i;
            }
        }

        return 0;
    }
}

/// <summary>An option over a 0–100% range stepped in fixed increments — the audio
/// volumes. Clamps at the ends rather than wrapping.</summary>
public sealed class SettingsPercentOption : SettingsOption
{
    private const float Step = 0.05f;

    private readonly Func<AppSettings, float> _read;
    private readonly Func<AppSettings, float, AppSettings> _write;

    public SettingsPercentOption(
        TranslationKey label,
        TranslationKey? sectionHeader,
        Func<AppSettings, float> read,
        Func<AppSettings, float, AppSettings> write)
        : base(label, sectionHeader, restartRequired: false)
    {
        _read = read;
        _write = write;
    }

    public override string Display(AppSettings settings) =>
        ((int)MathF.Round(_read(settings) * 100f)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";

    public override AppSettings Cycle(AppSettings settings, int direction)
    {
        var stepped = Math.Clamp(_read(settings) + (direction * Step), 0f, 1f);
        return _write(settings, stepped);
    }
}

/// <summary>A non-cycling row that navigates to a sub-screen when activated (Enter or
/// click). <see cref="Cycle"/> is inert; the owning screen reads <see cref="Target"/>
/// and performs the push, since that requires a <c>ScreenStack</c> the pure model layer
/// cannot hold.</summary>
public sealed class SettingsLinkOption : SettingsOption
{
    public SettingsLinkOption(TranslationKey label, TranslationKey? sectionHeader, SettingsLinkTarget target)
        : base(label, sectionHeader, restartRequired: false)
    {
        Target = target;
    }

    public SettingsLinkTarget Target { get; }

    public override string Display(AppSettings settings) => string.Empty;

    public override AppSettings Cycle(AppSettings settings, int direction) => settings;
}
