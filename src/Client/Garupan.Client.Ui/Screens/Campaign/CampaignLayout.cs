using System;
using Garupan.Content;

namespace Garupan.Client.Ui.Screens.Campaign;

/// <summary>
/// Pure layout maths for the campaign graph: maps a <see cref="CampaignNode"/>'s
/// normalised (X, Y) into pixel coordinates and answers the inverse question (which node
/// is under the mouse). No drawing, no input — both the renderer and the screen's
/// hit-test loop consume the same routines.
/// </summary>
public sealed class CampaignLayout
{
    private readonly CampaignSpec _campaign;

    public CampaignLayout(CampaignSpec campaign)
    {
        _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
    }

    public CampaignSpec Campaign => _campaign;

    public (int X, int Y) NodeCenter(int index, int surfaceWidth, int surfaceHeight)
    {
        var node = _campaign.Nodes[index];
        var usableW = surfaceWidth - (CampaignPalette.LayoutMarginX * 2);
        var usableH = surfaceHeight - CampaignPalette.LayoutMarginTop - CampaignPalette.LayoutMarginBottom;
        var x = CampaignPalette.LayoutMarginX + (int)(node.X * usableW);
        var y = CampaignPalette.LayoutMarginTop + (int)(node.Y * usableH);
        return (x, y);
    }

    public int HitTest(int mouseX, int mouseY, int surfaceWidth, int surfaceHeight)
    {
        if (surfaceWidth == 0 || surfaceHeight == 0)
        {
            return -1;
        }

        var r = CampaignPalette.NodeRadius;
        for (var i = 0; i < _campaign.Nodes.Count; i++)
        {
            var (cx, cy) = NodeCenter(i, surfaceWidth, surfaceHeight);
            if (mouseX >= cx - r && mouseX <= cx + r &&
                mouseY >= cy - r && mouseY <= cy + r)
            {
                return i;
            }
        }

        return -1;
    }

    public int FindNodeIndex(string missionId)
    {
        for (var i = 0; i < _campaign.Nodes.Count; i++)
        {
            if (string.Equals(_campaign.Nodes[i].MissionId, missionId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
