using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Text;
using Garupan.Content;
using Garupan.Localisation;
using Opus.Engine.Ui;
using Opus.Localisation;

namespace Garupan.Client.Ui.Screens.Campaign;

/// <summary>
/// Renders the bottom-strip detail panel that follows the hovered campaign node. No
/// hover → shows the "select mission" prompt; hovered → shows mission title, episode
/// reference, progression-status badge, and either the wrapped lore summary (unlocked)
/// or the "complete the prerequisite" hint (locked).
///
/// Stays separate from <see cref="CampaignGraphRenderer"/> because the panel will grow
/// substantially (per-mission preview thumbnails, status icons, performance grades) and
/// would otherwise dominate the graph renderer.
/// </summary>
public sealed class CampaignDetailPanelRenderer
{
    private const int PanelHeight = 130;

    private readonly LocalizationService _l10n;
    private readonly CampaignSpec _campaign;

    public CampaignDetailPanelRenderer(LocalizationService l10n, CampaignSpec campaign)
    {
        _l10n = l10n;
        _campaign = campaign;
    }

    public void Render(IDrawSurface surface, int hoveredIndex, CampaignProgressView progress)
    {
        var w = surface.Width;
        var h = surface.Height;

        var panelY = h - CampaignPalette.LayoutMarginBottom + 30;
        surface.FillRect(60, panelY, w - 120, PanelHeight, CampaignPalette.Panel);
        surface.FillRect(60, panelY, 4, PanelHeight, CampaignPalette.Crimson);

        if (hoveredIndex < 0)
        {
            surface.DrawText(
                _l10n.T(L10nKeys.Campaign.SelectMission),
                80, panelY + 18, 18, CampaignPalette.Dim);
            return;
        }

        var status = progress.StatusAt(hoveredIndex);
        if (status == CampaignNodeStatus.Hidden)
        {
            RenderHidden(surface, hoveredIndex, panelY, w);
            return;
        }

        var mission = _campaign.Missions[hoveredIndex];

        surface.DrawText(
            _l10n.T(TranslationKey.Of(mission.TitleKey)),
            80, panelY + 14, 22, CampaignPalette.Foreground);

        surface.DrawText(
            $"{mission.EpisodeReference}  •  {_l10n.T(StatusKey(status))}",
            80, panelY + 44, 13, StatusColor(status));

        var body = status == CampaignNodeStatus.Locked
            ? _l10n.T(L10nKeys.Campaign.LockedHint)
            : _l10n.T(TranslationKey.Of(mission.LoreSummaryKey));
        TextLayout.DrawWrapped(surface, 80, panelY + 68, w - 200, 14, body, CampaignPalette.Foreground);
    }

    /// <summary>Hidden-node detail strip — no title, no summary, no episode reference.
    /// The body text explains *why* the node is concealed without leaking which arc it
    /// belongs to.</summary>
    private void RenderHidden(IDrawSurface surface, int hoveredIndex, int panelY, int w)
    {
        surface.DrawText("???", 80, panelY + 14, 22, CampaignPalette.LockedText);
        surface.DrawText(
            _l10n.T(L10nKeys.Campaign.StatusHidden),
            80, panelY + 44, 13, CampaignPalette.LockedText);

        var hintKey = hoveredIndex >= CampaignProgressView.ArcBlockSize
            ? L10nKeys.Campaign.HiddenArcBlock
            : L10nKeys.Campaign.HiddenFirstHint;
        TextLayout.DrawWrapped(
            surface, 80, panelY + 68, w - 200, 14,
            _l10n.T(hintKey), CampaignPalette.LockedText);
    }

    private static TranslationKey StatusKey(CampaignNodeStatus status) => status switch
    {
        CampaignNodeStatus.Completed => L10nKeys.Campaign.StatusComplete,
        CampaignNodeStatus.Locked => L10nKeys.Campaign.StatusLocked,
        CampaignNodeStatus.Hidden => L10nKeys.Campaign.StatusHidden,
        _ => L10nKeys.Campaign.StatusAvailable,
    };

    private static Color StatusColor(CampaignNodeStatus status) => status switch
    {
        CampaignNodeStatus.Completed => CampaignPalette.Complete,
        CampaignNodeStatus.Locked => CampaignPalette.LockedText,
        CampaignNodeStatus.Hidden => CampaignPalette.LockedText,
        _ => CampaignPalette.Foreground,
    };
}
