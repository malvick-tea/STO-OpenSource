using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Text;
using Garupan.Content;
using Opus.Engine.Ui;
using Opus.Localisation;

namespace Garupan.Client.Ui.Screens.Campaign.Briefing;

/// <summary>
/// Right column of the briefing screen: deep-panel background, "BRIEFING" header,
/// wrapped long-form copy. Separate from <see cref="BriefingLeftPanel"/> because the
/// two columns evolve at different rates — the left side picks up status badges and
/// crew-loadout chips first, the right side eventually hosts the cinematic preview.
/// </summary>
public sealed class BriefingRightPanel
{
    private readonly LocalizationService _l10n;

    public BriefingRightPanel(LocalizationService l10n)
    {
        _l10n = l10n;
    }

    public void Render(IDrawSurface surface, MissionSpec mission)
    {
        var w = surface.Width;
        var h = surface.Height;
        var midX = w / 2;
        var contentTop = 80;
        var contentH = h - 160;

        surface.FillRect(midX + 20, contentTop, w - midX - 60, contentH, BriefingPalette.PanelDeep);
        surface.DrawText("BRIEFING", midX + 44, contentTop + 18, 14, BriefingPalette.Dim);
        surface.FillRect(midX + 44, contentTop + 38, 60, 2, BriefingPalette.Crimson);

        TextLayout.DrawWrapped(
            surface,
            midX + 44,
            contentTop + 60,
            w - midX - 110,
            17,
            _l10n.T(TranslationKey.Of(mission.BriefingKey)),
            BriefingPalette.Foreground);
    }
}
