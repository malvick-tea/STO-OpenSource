using Garupan.Client.Core.Services;
using Garupan.Localisation;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Settings;

/// <summary>
/// Draws <see cref="ControlsScreen"/>: top bar, one row per rebindable action (label plus
/// the current key, or a "press a key" prompt while that row is listening), and the
/// bottom hint. Pure read — the <see cref="ControlsModel"/> is passed in each frame.
/// </summary>
public sealed class ControlsRenderer
{
    private const int KeyFontSize = 18;

    private readonly LocalizationService _l10n;
    private readonly ControlsLayout _layout;

    public ControlsRenderer(LocalizationService l10n, ControlsLayout layout)
    {
        _l10n = l10n;
        _layout = layout;
    }

    public void Render(IDrawSurface surface, ControlsModel model)
    {
        surface.Clear(SettingsPalette.Background);
        var w = surface.Width;

        DrawTopBar(surface, w);
        for (var i = 0; i < model.Actions.Count; i++)
        {
            DrawRow(surface, w, i, model);
        }

        DrawHint(surface, w, surface.Height);
    }

    private void DrawTopBar(IDrawSurface s, int w)
    {
        s.FillRect(0, 0, w, SettingsPalette.TopBarHeight, SettingsPalette.Panel);
        s.DrawText(_l10n.T(L10nKeys.Settings.TabControls), 24, 14, 22, SettingsPalette.Foreground);
        s.FillRect(0, SettingsPalette.TopBarHeight, w, 2, SettingsPalette.Crimson);
    }

    private void DrawRow(IDrawSurface s, int w, int index, ControlsModel model)
    {
        var top = _layout.RowTop(index);
        var selected = index == model.SelectedRow;
        var listening = index == model.ListeningRow;

        if (selected)
        {
            var px = SettingsPalette.MarginX - 20;
            s.FillRect(px, top + 4, w - (px * 2), SettingsPalette.RowHeight - 8, SettingsPalette.RowSelected);
            s.FillRect(px, top + 4, 4, SettingsPalette.RowHeight - 8, SettingsPalette.Crimson);
        }

        var labelColor = selected ? SettingsPalette.Foreground : SettingsPalette.Dim;
        s.DrawText(
            _l10n.T(model.Actions[index].Label),
            SettingsPalette.MarginX, top + 14, SettingsPalette.LabelFontSize, labelColor);

        var keyText = listening
            ? _l10n.T(L10nKeys.Controls.Listening)
            : model.Actions[index].Read(model.Bindings).ToString();
        var keyColor = listening
            ? SettingsPalette.Crimson
            : (selected ? SettingsPalette.Foreground : SettingsPalette.Dim);
        var centerX = w - SettingsPalette.MarginX - (SettingsPalette.ValueAreaWidth / 2);
        s.DrawText(keyText, centerX - (s.MeasureText(keyText, KeyFontSize) / 2), top + 14, KeyFontSize, keyColor);
    }

    private void DrawHint(IDrawSurface s, int w, int h)
    {
        var hint = _l10n.T(L10nKeys.Controls.Hint);
        s.DrawText(hint, (w - s.MeasureText(hint, 12)) / 2, h - 30, 12, SettingsPalette.Dim);
    }
}
