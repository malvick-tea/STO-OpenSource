using Garupan.Client.Core.Services;
using Garupan.Localisation;
using Opus.Engine.Ui;
using Opus.Localisation;

namespace Garupan.Client.Ui.Screens.Settings.Multiplayer;

/// <summary>
/// Draws <see cref="MultiplayerSettingsScreen"/>: top bar, three stacked endpoint
/// sections (default + Hungry Battles override + Tactical 5v5 override), each with a
/// section header and two field rows (label + value + caret). A validity pill sits
/// below the last section; the hint line sits at the screen bottom. Pure read — the
/// <see cref="MultiplayerSettingsModel"/> is passed in each frame; no renderer state
/// survives across frames except the blink counter for the caret.
/// </summary>
public sealed class MultiplayerSettingsRenderer
{
    private const int ValueFontSize = 18;
    private const int PillFontSize = 12;
    private const int SectionHeaderFontSize = 14;
    private const int CaretBlinkPeriodFrames = 30;

    private readonly LocalizationService _l10n;
    private readonly MultiplayerSettingsLayout _layout;
    private int _frameCounter;

    public MultiplayerSettingsRenderer(LocalizationService l10n, MultiplayerSettingsLayout layout)
    {
        _l10n = l10n;
        _layout = layout;
    }

    public void Render(IDrawSurface surface, MultiplayerSettingsModel model)
    {
        _frameCounter++;
        surface.Clear(SettingsPalette.Background);
        var w = surface.Width;

        DrawTopBar(surface, w);
        DrawSection(surface, w, model, MultiplayerSettingsModel.SectionDefault, L10nKeys.Settings.Multiplayer.SectionDefault, isOverride: false);
        DrawSection(surface, w, model, MultiplayerSettingsModel.SectionHungry, L10nKeys.Settings.Multiplayer.SectionHungry, isOverride: true);
        DrawSection(surface, w, model, MultiplayerSettingsModel.SectionTactical, L10nKeys.Settings.Multiplayer.SectionTactical, isOverride: true);
        DrawValidityPill(surface, w, model.IsValid);
        DrawHint(surface, w, surface.Height);
    }

    private void DrawTopBar(IDrawSurface s, int w)
    {
        s.FillRect(0, 0, w, SettingsPalette.TopBarHeight, SettingsPalette.Panel);
        s.DrawText(_l10n.T(L10nKeys.Settings.TabMultiplayer), 24, 14, 22, SettingsPalette.Foreground);
        s.FillRect(0, SettingsPalette.TopBarHeight, w, 2, SettingsPalette.Crimson);
    }

    private void DrawSection(
        IDrawSurface s,
        int w,
        MultiplayerSettingsModel model,
        int section,
        TranslationKey sectionLabelKey,
        bool isOverride)
    {
        var headerTop = _layout.SectionHeaderTop(section);
        s.DrawText(
            _l10n.T(sectionLabelKey),
            SettingsPalette.MarginX - 12,
            headerTop + 8,
            SectionHeaderFontSize,
            SettingsPalette.Crimson);
        s.FillRect(
            SettingsPalette.MarginX - 24,
            headerTop + SectionHeaderFontSize + 12,
            w - ((SettingsPalette.MarginX - 24) * 2),
            1,
            SettingsPalette.Dim);

        var hostFieldIndex = (section * MultiplayerSettingsModel.FieldsPerSection) + MultiplayerSettingsModel.HostFieldIndex;
        var portFieldIndex = (section * MultiplayerSettingsModel.FieldsPerSection) + MultiplayerSettingsModel.PortFieldIndex;
        DrawField(s, w, hostFieldIndex, _l10n.T(L10nKeys.Settings.Multiplayer.Host), model, isOverride);
        DrawField(s, w, portFieldIndex, _l10n.T(L10nKeys.Settings.Multiplayer.Port), model, isOverride);
    }

    private void DrawField(
        IDrawSurface s,
        int w,
        int index,
        string label,
        MultiplayerSettingsModel model,
        bool isOverride)
    {
        var top = _layout.RowTop(index);
        var focused = index == model.SelectedField;
        var field = model.Field(index);

        if (focused)
        {
            var px = SettingsPalette.MarginX - 20;
            s.FillRect(px, top + 4, w - (px * 2), SettingsPalette.RowHeight - 8, SettingsPalette.RowSelected);
            s.FillRect(px, top + 4, 4, SettingsPalette.RowHeight - 8, SettingsPalette.Crimson);
        }

        var labelColor = focused ? SettingsPalette.Foreground : SettingsPalette.Dim;
        s.DrawText(label, SettingsPalette.MarginX, top + 14, SettingsPalette.LabelFontSize, labelColor);

        DrawFieldValue(s, w, top, field, focused, isOverride);
    }

    private void DrawFieldValue(IDrawSurface s, int w, int top, TextInputField field, bool focused, bool isOverride)
    {
        var emptyValue = isOverride ? _l10n.T(L10nKeys.Settings.Multiplayer.UseDefault) : "—";
        var value = field.IsEmpty ? emptyValue : field.Value;
        var valueColor = focused
            ? SettingsPalette.Foreground
            : (field.IsEmpty ? SettingsPalette.Dim : SettingsPalette.Foreground);

        var x = w - SettingsPalette.MarginX - s.MeasureText(value, ValueFontSize);
        var y = top + 14;
        s.DrawText(value, x, y, ValueFontSize, valueColor);

        if (focused && !field.IsEmpty)
        {
            DrawCaret(s, value, x, y, field.Cursor);
        }
        else if (focused && field.IsEmpty)
        {
            DrawCaret(s, "0", x, y, 0);
        }
    }

    private void DrawCaret(IDrawSurface s, string value, int valueX, int valueY, int cursor)
    {
        if ((_frameCounter / CaretBlinkPeriodFrames) % 2 != 0)
        {
            return;
        }

        var prefix = cursor == 0 ? string.Empty : value[..cursor];
        var prefixWidth = s.MeasureText(prefix, ValueFontSize);
        s.FillRect(valueX + prefixWidth, valueY, 2, ValueFontSize, SettingsPalette.Foreground);
    }

    private void DrawValidityPill(IDrawSurface s, int w, bool isValid)
    {
        var text = isValid
            ? _l10n.T(L10nKeys.Settings.Multiplayer.Valid)
            : _l10n.T(L10nKeys.Settings.Multiplayer.Invalid);
        var color = isValid ? SettingsPalette.Section : SettingsPalette.Restart;

        var textWidth = s.MeasureText(text, PillFontSize);
        var x = (w - textWidth) / 2;
        var y = _layout.ContentBottom + 16;
        s.DrawText(text, x, y, PillFontSize, color);
    }

    private void DrawHint(IDrawSurface s, int w, int h)
    {
        var hint = _l10n.T(L10nKeys.Settings.Multiplayer.Hint);
        s.DrawText(hint, (w - s.MeasureText(hint, 12)) / 2, h - 30, 12, SettingsPalette.Dim);
    }
}
