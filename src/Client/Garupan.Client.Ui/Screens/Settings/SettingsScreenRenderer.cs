using Garupan.Client.Core.Services;
using Garupan.Localisation;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Settings;

/// <summary>
/// Draws the settings screen: top bar, the option rows (section headers, selection
/// highlight, label, value, ‹ › chevrons, restart tag), and the bottom hint. Pure read —
/// the <see cref="SettingsModel"/> is passed in each frame; this type holds no state.
/// </summary>
public sealed class SettingsScreenRenderer
{
    private const int ValueFontSize = 18;

    private readonly LocalizationService _l10n;
    private readonly SettingsLayout _layout;

    public SettingsScreenRenderer(LocalizationService l10n, SettingsLayout layout)
    {
        _l10n = l10n;
        _layout = layout;
    }

    public void Render(IDrawSurface surface, SettingsModel model)
    {
        surface.Clear(SettingsPalette.Background);
        var w = surface.Width;

        DrawTopBar(surface, w);
        for (var i = 0; i < model.Options.Count; i++)
        {
            DrawRow(surface, w, i, model);
        }

        DrawHint(surface, w, surface.Height);
    }

    private void DrawTopBar(IDrawSurface s, int w)
    {
        s.FillRect(0, 0, w, SettingsPalette.TopBarHeight, SettingsPalette.Panel);
        s.DrawText(_l10n.T(L10nKeys.Settings.Title), 24, 14, 22, SettingsPalette.Foreground);
        s.FillRect(0, SettingsPalette.TopBarHeight, w, 2, SettingsPalette.Crimson);
    }

    private void DrawRow(IDrawSurface s, int w, int index, SettingsModel model)
    {
        var option = model.Options[index];
        var top = _layout.RowTop(index);
        var selected = index == model.SelectedRow;

        if (option.SectionHeader is { } section)
        {
            s.DrawText(
                _l10n.T(section).ToUpperInvariant(),
                SettingsPalette.MarginX, _layout.SectionHeaderY(index), 13, SettingsPalette.Section);
        }

        if (selected)
        {
            var px = SettingsPalette.MarginX - 20;
            s.FillRect(px, top + 4, w - (px * 2), SettingsPalette.RowHeight - 8, SettingsPalette.RowSelected);
            s.FillRect(px, top + 4, 4, SettingsPalette.RowHeight - 8, SettingsPalette.Crimson);
        }

        var labelColor = selected ? SettingsPalette.Foreground : SettingsPalette.Dim;
        s.DrawText(_l10n.T(option.Label), SettingsPalette.MarginX, top + 9, SettingsPalette.LabelFontSize, labelColor);

        if (option.RestartRequired)
        {
            s.DrawText(_l10n.T(L10nKeys.Settings.Restart), SettingsPalette.MarginX, top + 32, 11, SettingsPalette.Restart);
        }

        if (option is SettingsLinkOption)
        {
            var arrowColor = selected ? SettingsPalette.ArrowActive : SettingsPalette.Arrow;
            DrawChevron(
                s, _layout.RightArrowX(w) + (SettingsPalette.ArrowWidth / 2),
                top + (SettingsPalette.RowHeight / 2), +1, arrowColor);
        }
        else
        {
            DrawValue(s, w, top, option.Display(model.Current), selected);
        }
    }

    private void DrawValue(IDrawSurface s, int w, int top, string value, bool selected)
    {
        var midY = top + (SettingsPalette.RowHeight / 2);
        var valueColor = selected ? SettingsPalette.Foreground : SettingsPalette.Dim;
        var arrowColor = selected ? SettingsPalette.ArrowActive : SettingsPalette.Arrow;

        DrawChevron(s, _layout.LeftArrowX(w) + (SettingsPalette.ArrowWidth / 2), midY, -1, arrowColor);
        DrawChevron(s, _layout.RightArrowX(w) + (SettingsPalette.ArrowWidth / 2), midY, +1, arrowColor);

        var valueWidth = s.MeasureText(value, ValueFontSize);
        s.DrawText(value, _layout.ValueCenterX(w) - (valueWidth / 2), top + 14, ValueFontSize, valueColor);
    }

    private static void DrawChevron(IDrawSurface s, int centerX, int centerY, int direction, Color color)
    {
        var tipX = centerX - (direction * 5);
        var armX = centerX + (direction * 5);
        s.DrawLine(tipX, centerY, armX, centerY - 8, 2, color);
        s.DrawLine(tipX, centerY, armX, centerY + 8, 2, color);
    }

    private void DrawHint(IDrawSurface s, int w, int h)
    {
        var hint = _l10n.T(L10nKeys.Settings.Hint);
        s.DrawText(hint, (w - s.MeasureText(hint, 12)) / 2, h - 30, 12, SettingsPalette.Dim);
    }
}
