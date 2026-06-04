using System.Collections.Generic;
using System.Globalization;
using Garupan.Client.Core.Services;
using Garupan.Content;
using Garupan.Localisation;
using Opus.Engine.Ui;
using Opus.Localisation;

namespace Garupan.Client.Ui.Screens.Campaign;

/// <summary>
/// Draws the campaign-graph chrome: top bar with title, campaign subtitle, the
/// prerequisite edges, the mission nodes (with hover highlight + progression status),
/// and the bottom hint. Pure read — the hovered index and the
/// <see cref="CampaignProgressView"/> are passed in from the screen each frame.
///
/// The detail panel is rendered by a separate <see cref="CampaignDetailPanelRenderer"/>
/// because its layout grows independently (mission summary, status badges, eventually
/// a per-mission preview thumbnail) and shouldn't bloat this drawer.
/// </summary>
public sealed class CampaignGraphRenderer
{
    private const string HintText = "Hover to read • Click to open briefing • Esc to back out";

    private readonly LocalizationService _l10n;
    private readonly CampaignLayout _layout;

    public CampaignGraphRenderer(LocalizationService l10n, CampaignLayout layout)
    {
        _l10n = l10n;
        _layout = layout;
    }

    public void Render(IDrawSurface surface, int hoveredIndex, CampaignProgressView progress)
    {
        var w = surface.Width;
        var h = surface.Height;

        DrawTopBar(surface, w);
        DrawSubtitle(surface, w);
        DrawEdges(surface, w, h, progress);
        DrawNodes(surface, w, h, hoveredIndex, progress);
        DrawHint(surface, w, h);
    }

    private void DrawTopBar(IDrawSurface s, int w)
    {
        s.FillRect(0, 0, w, 56, CampaignPalette.Panel);
        s.DrawText(_l10n.T(L10nKeys.Campaign.Title), 24, 14, 22, CampaignPalette.Foreground);
        s.FillRect(0, 56, w, 2, CampaignPalette.Crimson);
    }

    private void DrawSubtitle(IDrawSurface s, int w)
    {
        var campaign = _layout.Campaign;
        var name = _l10n.T(TranslationKey.Of(campaign.NameKey));
        var subtitle = _l10n.T(TranslationKey.Of(campaign.ShortDescriptionKey));
        s.DrawText(name, (w - s.MeasureText(name, 26)) / 2, 70, 26, CampaignPalette.Foreground);
        s.DrawText(subtitle, (w - s.MeasureText(subtitle, 14)) / 2, 100, 14, CampaignPalette.Dim);
    }

    private void DrawEdges(IDrawSurface s, int w, int h, CampaignProgressView progress)
    {
        var nodes = _layout.Campaign.Nodes;
        var r = CampaignPalette.NodeRadius;
        for (var i = 0; i < nodes.Count; i++)
        {
            foreach (var pre in nodes[i].Prerequisites)
            {
                var fromIdx = _layout.FindNodeIndex(pre);
                if (fromIdx < 0)
                {
                    continue;
                }

                // A cleared prerequisite lights its edge as a progression trail.
                var color = progress.StatusAt(fromIdx) == CampaignNodeStatus.Completed
                    ? CampaignPalette.CompleteEdge
                    : CampaignPalette.Edge;
                var (fx, fy) = _layout.NodeCenter(fromIdx, w, h);
                var (tx, ty) = _layout.NodeCenter(i, w, h);
                s.DrawLine(fx + r, fy, tx - r, ty, 2, color);
            }
        }
    }

    private void DrawNodes(IDrawSurface s, int w, int h, int hoveredIndex, CampaignProgressView progress)
    {
        var nodes = _layout.Campaign.Nodes;
        var missions = _layout.Campaign.Missions;
        var r = CampaignPalette.NodeRadius;

        for (var i = 0; i < nodes.Count; i++)
        {
            var (cx, cy) = _layout.NodeCenter(i, w, h);
            var status = progress.StatusAt(i);
            var hovered = i == hoveredIndex;

            s.FillRect(cx - r, cy - r, r * 2, r * 2, NodeFill(status, hovered));
            s.StrokeRect(cx - r, cy - r, r * 2, r * 2, 2, NodeStroke(status, hovered));

            var textColor = NodeTextColor(status, hovered);
            if (status == CampaignNodeStatus.Hidden)
            {
                DrawHiddenNode(s, cx, cy, r, textColor);
            }
            else
            {
                DrawRevealedNode(s, cx, cy, r, i, missions, status, textColor);
            }

            DrawStatusBadge(s, cx + r - 9, cy - r + 9, status);
        }
    }

    private void DrawRevealedNode(
        IDrawSurface s,
        int cx,
        int cy,
        int r,
        int nodeIndex,
        IReadOnlyList<MissionSpec> missions,
        CampaignNodeStatus status,
        Color textColor)
    {
        var num = (nodeIndex + 1).ToString(CultureInfo.InvariantCulture);
        var numSize = s.MeasureText(num, 20);
        s.DrawText(num, cx - numSize / 2, cy - 13, 20, textColor);

        var label = _l10n.T(TranslationKey.Of(missions[nodeIndex].TitleKey));
        var labelSize = s.MeasureText(label, 14);
        s.DrawText(label, cx - labelSize / 2, cy + r + 8, 14, textColor);

        var ep = missions[nodeIndex].EpisodeReference;
        var epSize = s.MeasureText(ep, 11);
        var epColor = status == CampaignNodeStatus.Locked ? CampaignPalette.LockedText : CampaignPalette.Dim;
        s.DrawText(ep, cx - epSize / 2, cy + r + 28, 11, epColor);
    }

    private static void DrawHiddenNode(IDrawSurface s, int cx, int cy, int r, Color textColor)
    {
        const string Mask = "???";
        var maskSize = s.MeasureText(Mask, 20);
        s.DrawText(Mask, cx - maskSize / 2, cy - 13, 20, textColor);

        // No mission label or episode reference under the node — the whole point of
        // Hidden is that the player learns nothing about the operation in advance.
    }

    private static Color NodeFill(CampaignNodeStatus status, bool hovered)
    {
        if (hovered && status != CampaignNodeStatus.Hidden)
        {
            return CampaignPalette.PanelHover;
        }

        return status switch
        {
            CampaignNodeStatus.Hidden => CampaignPalette.LockedFill,
            CampaignNodeStatus.Locked => CampaignPalette.LockedFill,
            _ => CampaignPalette.Panel,
        };
    }

    private static Color NodeStroke(CampaignNodeStatus status, bool hovered)
    {
        return status switch
        {
            CampaignNodeStatus.Completed => CampaignPalette.CompleteEdge,
            CampaignNodeStatus.Available => hovered ? CampaignPalette.EdgeBright : CampaignPalette.Edge,
            _ => CampaignPalette.Edge,
        };
    }

    private static Color NodeTextColor(CampaignNodeStatus status, bool hovered)
    {
        return status switch
        {
            CampaignNodeStatus.Completed => CampaignPalette.Foreground,
            CampaignNodeStatus.Locked => CampaignPalette.LockedText,
            CampaignNodeStatus.Hidden => CampaignPalette.LockedText,
            _ => hovered ? CampaignPalette.Foreground : CampaignPalette.Dim,
        };
    }

    /// <summary>Top-right corner glyph: a padlock for locked nodes, a check for cleared
    /// ones, nothing for an available node (its hover highlight carries the cue).
    /// Hidden nodes get no badge — the centre <c>???</c> already tells the story and a
    /// second cue would compete for the eye.</summary>
    private static void DrawStatusBadge(IDrawSurface s, int gx, int gy, CampaignNodeStatus status)
    {
        switch (status)
        {
            case CampaignNodeStatus.Locked:
                s.StrokeCircle(gx, gy - 2, 4, 2, CampaignPalette.LockedText);
                s.FillRect(gx - 5, gy, 11, 8, CampaignPalette.LockedText);
                break;

            case CampaignNodeStatus.Completed:
                s.DrawLine(gx - 5, gy + 1, gx - 1, gy + 5, 3, CampaignPalette.Complete);
                s.DrawLine(gx - 1, gy + 5, gx + 6, gy - 5, 3, CampaignPalette.Complete);
                break;

            case CampaignNodeStatus.Available:
            case CampaignNodeStatus.Hidden:
            default:
                break;
        }
    }

    private static void DrawHint(IDrawSurface s, int w, int h)
    {
        var hintSize = s.MeasureText(HintText, 12);
        s.DrawText(HintText, (w - hintSize) / 2, h - 22, 12, CampaignPalette.Dim);
    }
}
