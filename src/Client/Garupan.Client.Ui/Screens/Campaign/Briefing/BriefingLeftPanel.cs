using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Text;
using Garupan.Content;
using Garupan.Localisation;
using Opus.Engine.Ui;
using Opus.Localisation;

namespace Garupan.Client.Ui.Screens.Campaign.Briefing;

/// <summary>
/// Left column of the briefing screen: panel + title + episode + meta rows
/// (opponent / environment / objective) + wrapped lore summary. The DEPLOY button on
/// the same column is owned by <see cref="DeployButton"/> so its hit-test stays bundled
/// with its render.
/// </summary>
public sealed class BriefingLeftPanel
{
    private readonly LocalizationService _l10n;

    public BriefingLeftPanel(LocalizationService l10n)
    {
        _l10n = l10n;
    }

    public void Render(IDrawSurface surface, MissionSpec mission)
    {
        var w = surface.Width;
        var h = surface.Height;
        var midX = w / 2;
        var contentTop = 80;
        var contentH = h - 160; // 80 top + 80 bottom

        surface.FillRect(40, contentTop, midX - 60, contentH, BriefingPalette.Panel);
        surface.FillRect(40, contentTop, 4, contentH, BriefingPalette.Crimson);

        surface.DrawText(
            _l10n.T(TranslationKey.Of(mission.TitleKey)),
            64, contentTop + 18, 30, BriefingPalette.Foreground);
        surface.DrawText(mission.EpisodeReference, 64, contentTop + 56, 14, BriefingPalette.Dim);
        surface.FillRect(64, contentTop + 76, 80, 2, BriefingPalette.Crimson);

        var metaY = contentTop + 100;
        DrawMetaRow(surface, 64, metaY,       L10nKeys.Campaign.BriefingOpponent,    SchoolNames.Resolve(_l10n, mission.Opponent));
        DrawMetaRow(surface, 64, metaY + 32,  L10nKeys.Campaign.BriefingEnvironment, mission.Environment.ToString());
        DrawMetaRow(surface, 64, metaY + 64,  L10nKeys.Campaign.BriefingObjective,   mission.Objective.ToString());

        var summaryY = metaY + 120;
        TextLayout.DrawWrapped(
            surface,
            64,
            summaryY,
            midX - 110,
            16,
            _l10n.T(TranslationKey.Of(mission.LoreSummaryKey)),
            BriefingPalette.Foreground);
    }

    private void DrawMetaRow(IDrawSurface surface, int x, int y, TranslationKey labelKey, string value)
    {
        var label = _l10n.T(labelKey);
        surface.DrawText(label.ToUpperInvariant(), x, y, 12, BriefingPalette.Dim);
        surface.DrawText(value, x, y + 16, 16, BriefingPalette.Foreground);
    }
}
