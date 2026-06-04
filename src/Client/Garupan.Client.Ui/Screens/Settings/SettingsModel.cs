using System;
using System.Collections.Generic;
using Garupan.Client.Core.Application;
using Garupan.Localisation;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Settings;

/// <summary>
/// The settings screen's data model: the ordered list of <see cref="SettingsOption"/>
/// rows, the live <see cref="AppSettings"/> snapshot, and the selection cursor. Pure —
/// no service or VFS dependency, so it is unit-testable directly. The screen owns the
/// bridge to <c>SettingsService</c> / <c>LocalizationService</c> via the
/// <see cref="Changed"/> event.
/// </summary>
public sealed class SettingsModel
{
    private static readonly (int Width, int Height)[] Resolutions =
    {
        (1280, 720), (1600, 900), (1920, 1080), (2560, 1440),
    };

    private static readonly IReadOnlyDictionary<string, string> LocaleEndonyms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "English",
            ["ru"] = "Русский",
            ["ja"] = "日本語",
        };

    private readonly IReadOnlyList<SettingsOption> _options;
    private AppSettings _current;
    private int _selectedRow;

    public SettingsModel(AppSettings initial, IReadOnlyList<string> availableLocales)
    {
        _current = Ensure.NotNull(initial);
        Ensure.NotNull(availableLocales);
        _options = BuildOptions(availableLocales);
    }

    /// <summary>Raised once per coherent value change, carrying the new snapshot.</summary>
    public event Action<AppSettings>? Changed;

    public AppSettings Current => _current;

    public IReadOnlyList<SettingsOption> Options => _options;

    public int SelectedRow => _selectedRow;

    /// <summary>Moves the row cursor by ±1, clamped to the option list bounds.</summary>
    public void MoveSelection(int direction)
    {
        _selectedRow = Math.Clamp(_selectedRow + Math.Sign(direction), 0, _options.Count - 1);
    }

    /// <summary>Parks the cursor on <paramref name="row"/> (e.g. follows the mouse). Out-of-range rows are ignored.</summary>
    public void SelectRow(int row)
    {
        if (row >= 0 && row < _options.Count)
        {
            _selectedRow = row;
        }
    }

    /// <summary>Steps the selected option by ±1. Raises <see cref="Changed"/> only when
    /// the snapshot actually moved (a clamped no-op stays silent).</summary>
    public void AdjustSelected(int direction)
    {
        var step = Math.Sign(direction);
        if (step == 0)
        {
            return;
        }

        var next = _options[_selectedRow].Cycle(_current, step);
        if (next == _current)
        {
            return;
        }

        _current = next;
        Changed?.Invoke(_current);
    }

    private static IReadOnlyList<SettingsOption> BuildOptions(IReadOnlyList<string> availableLocales)
    {
        var options = new List<SettingsOption>
        {
            new SettingsChoiceOption(
                L10nKeys.Settings.VSync, L10nKeys.Settings.TabGraphics, restartRequired: true, VSyncChoices()),
            new SettingsChoiceOption(
                L10nKeys.Settings.Resolution, sectionHeader: null, restartRequired: true, ResolutionChoices()),
            new SettingsPercentOption(
                L10nKeys.Settings.MasterVolume, L10nKeys.Settings.TabAudio,
                s => s.MasterVolume, (s, v) => s with { MasterVolume = v }),
            new SettingsPercentOption(
                L10nKeys.Settings.MusicVolume, sectionHeader: null,
                s => s.MusicVolume, (s, v) => s with { MusicVolume = v }),
            new SettingsPercentOption(
                L10nKeys.Settings.SfxVolume, sectionHeader: null,
                s => s.SfxVolume, (s, v) => s with { SfxVolume = v }),
        };

        var localeChoices = LocaleChoices(availableLocales);
        if (localeChoices.Count > 0)
        {
            options.Add(new SettingsChoiceOption(
                L10nKeys.Settings.Locale, L10nKeys.Settings.TabLanguage, restartRequired: false, localeChoices));
        }

        options.Add(new SettingsLinkOption(L10nKeys.Controls.Edit, L10nKeys.Settings.TabControls, SettingsLinkTarget.Controls));
        options.Add(new SettingsLinkOption(L10nKeys.Settings.Multiplayer.Edit, L10nKeys.Settings.TabMultiplayer, SettingsLinkTarget.Multiplayer));

        return options;
    }

    private static IReadOnlyList<SettingsChoice> VSyncChoices() => new[]
    {
        new SettingsChoice("Off", s => !s.VSync, s => s with { VSync = false }),
        new SettingsChoice("On", s => s.VSync, s => s with { VSync = true }),
    };

    private static IReadOnlyList<SettingsChoice> ResolutionChoices()
    {
        var choices = new List<SettingsChoice>(Resolutions.Length);
        foreach (var (width, height) in Resolutions)
        {
            var w = width;
            var h = height;
            choices.Add(new SettingsChoice(
                $"{w} × {h}",
                s => s.WindowWidth == w && s.WindowHeight == h,
                s => s with { WindowWidth = w, WindowHeight = h }));
        }

        return choices;
    }

    private static IReadOnlyList<SettingsChoice> LocaleChoices(IReadOnlyList<string> availableLocales)
    {
        var choices = new List<SettingsChoice>(availableLocales.Count);
        foreach (var locale in availableLocales)
        {
            var code = locale;
            var display = LocaleEndonyms.TryGetValue(code, out var endonym)
                ? endonym
                : code.ToUpperInvariant();
            choices.Add(new SettingsChoice(
                display,
                s => string.Equals(s.Locale, code, StringComparison.OrdinalIgnoreCase),
                s => s with { Locale = code }));
        }

        return choices;
    }
}
