using System.Collections.Generic;
using Garupan.Content;

namespace Garupan.Client.Core.Services;

/// <summary>
/// Pure prerequisite-resolution rule shared by <see cref="CampaignProgressService"/>
/// (single-mission queries) and the campaign-screen progress view (whole-graph status).
/// Kept as a free function so both callers apply identical unlock semantics — a node is
/// unlocked when every prerequisite mission has been completed.
/// </summary>
public static class CampaignUnlock
{
    /// <summary>
    /// Whether <paramref name="node"/> is unlocked given the set of completed mission
    /// ids. A node with no prerequisites is always unlocked.
    /// </summary>
    public static bool IsNodeUnlocked(CampaignNode node, IReadOnlySet<string> completedMissionIds)
    {
        foreach (var prerequisite in node.Prerequisites)
        {
            if (!completedMissionIds.Contains(prerequisite, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
