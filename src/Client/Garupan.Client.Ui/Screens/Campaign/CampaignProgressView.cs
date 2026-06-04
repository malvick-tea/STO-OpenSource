using System.Collections.Generic;
using Garupan.Client.Core.Application;
using Garupan.Client.Core.Services;
using Garupan.Content;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Campaign;

/// <summary>
/// Read model that maps each campaign node to a <see cref="CampaignNodeStatus"/> from a
/// <see cref="CampaignProgress"/> snapshot. Pure — holds no service reference, so it is
/// unit-testable with hand-built progress records. <see cref="CampaignScreen"/> owns the
/// service and feeds fresh snapshots in via <see cref="Sync"/>.
/// </summary>
/// <remarks>
/// <para>
/// Discovery gating (introduced 2026-05-20 alongside the campaign expansion past the player commander's
/// arc):
/// </para>
/// <list type="bullet">
/// <item>The player sees the first node always. Everything past it is <see cref="CampaignNodeStatus.Hidden"/>
/// until the first mission is cleared — title and summary are masked under <c>???</c>.</item>
/// <item>The arc ships in twelve-mission blocks. Until the player clears every node in
/// the first block, every node from index <see cref="ArcBlockSize"/> onwards stays
/// hidden — no peeking at the next girls' arcs before the player commander's is closed.</item>
/// <item>Below that, the existing prerequisite chain decides Available vs. Locked
/// exactly as before.</item>
/// </list>
/// </remarks>
public sealed class CampaignProgressView
{
    /// <summary>Size of the canonical first arc block (the player commander's 12-mission canon run).
    /// Nodes past this index stay hidden until every node in the first block is
    /// completed.</summary>
    public const int ArcBlockSize = 12;

    private readonly CampaignSpec _campaign;
    private readonly CampaignNodeStatus[] _statuses;
    private CampaignProgress _source;

    public CampaignProgressView(CampaignSpec campaign, CampaignProgress initial)
    {
        _campaign = Ensure.NotNull(campaign);
        _source = Ensure.NotNull(initial);
        _statuses = new CampaignNodeStatus[campaign.Nodes.Count];
        Recompute();
    }

    /// <summary>Status of the node at <paramref name="nodeIndex"/> (parallel to
    /// <see cref="CampaignSpec.Nodes"/> / <see cref="CampaignSpec.Missions"/>).</summary>
    public CampaignNodeStatus StatusAt(int nodeIndex) => _statuses[nodeIndex];

    /// <summary>Whether the node can be opened — only <see cref="CampaignNodeStatus.Available"/>
    /// and <see cref="CampaignNodeStatus.Completed"/> nodes accept clicks. Hidden + Locked
    /// reject input.</summary>
    public bool IsPlayable(int nodeIndex) =>
        _statuses[nodeIndex] == CampaignNodeStatus.Available ||
        _statuses[nodeIndex] == CampaignNodeStatus.Completed;

    /// <summary>True when the node is concealed and its mission details must not leak
    /// to the UI (mask title / summary / episode under <c>???</c>).</summary>
    public bool IsHidden(int nodeIndex) => _statuses[nodeIndex] == CampaignNodeStatus.Hidden;

    /// <summary>
    /// Recomputes the status table if <paramref name="current"/> is a different snapshot
    /// than the one last computed from. <see cref="CampaignProgress"/> is immutable, so a
    /// reference change is a real progress change. Returns <c>true</c> when a recompute ran.
    /// </summary>
    public bool Sync(CampaignProgress current)
    {
        Ensure.NotNull(current);
        if (ReferenceEquals(current, _source))
        {
            return false;
        }

        _source = current;
        Recompute();
        return true;
    }

    private void Recompute()
    {
        var completed = _source.CompletedMissionIds;
        var firstNodeCompleted = FirstNodeCompleted(completed);
        var firstBlockCompleted = FirstBlockCompleted(completed);

        for (var i = 0; i < _campaign.Nodes.Count; i++)
        {
            _statuses[i] = ResolveStatus(i, completed, firstNodeCompleted, firstBlockCompleted);
        }
    }

    private CampaignNodeStatus ResolveStatus(
        int nodeIndex,
        IReadOnlySet<string> completed,
        bool firstNodeCompleted,
        bool firstBlockCompleted)
    {
        var node = _campaign.Nodes[nodeIndex];
        if (completed.Contains(node.MissionId, StringComparer.Ordinal))
        {
            return CampaignNodeStatus.Completed;
        }

        if (nodeIndex == 0)
        {
            return CampaignUnlock.IsNodeUnlocked(node, completed)
                ? CampaignNodeStatus.Available
                : CampaignNodeStatus.Locked;
        }

        if (!firstNodeCompleted)
        {
            return CampaignNodeStatus.Hidden;
        }

        if (nodeIndex >= ArcBlockSize && !firstBlockCompleted)
        {
            return CampaignNodeStatus.Hidden;
        }

        return CampaignUnlock.IsNodeUnlocked(node, completed)
            ? CampaignNodeStatus.Available
            : CampaignNodeStatus.Locked;
    }

    private bool FirstNodeCompleted(IReadOnlySet<string> completed) =>
        _campaign.Nodes.Count == 0 || completed.Contains(_campaign.Nodes[0].MissionId, StringComparer.Ordinal);

    private bool FirstBlockCompleted(IReadOnlySet<string> completed)
    {
        var limit = System.Math.Min(ArcBlockSize, _campaign.Nodes.Count);
        for (var i = 0; i < limit; i++)
        {
            if (!completed.Contains(_campaign.Nodes[i].MissionId, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
