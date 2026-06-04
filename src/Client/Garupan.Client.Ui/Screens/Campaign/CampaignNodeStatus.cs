namespace Garupan.Client.Ui.Screens.Campaign;

/// <summary>
/// Per-node progression state on the campaign graph. Drives both the node's appearance
/// (<see cref="CampaignGraphRenderer"/>) and whether a click opens its briefing
/// (<see cref="CampaignScreen"/>).
/// </summary>
public enum CampaignNodeStatus
{
    /// <summary>Concealed entirely — title + summary masked behind <c>???</c>. Used when
    /// the player has not yet cleared the first mission of the arc (every subsequent
    /// node is hidden), and for any node past the 12-mission block until those twelve
    /// missions are all complete.</summary>
    Hidden,

    /// <summary>Prerequisite missions are not all complete — title is visible but the
    /// node is not playable.</summary>
    Locked,

    /// <summary>Unlocked and not yet cleared — the next thing the player can do.</summary>
    Available,

    /// <summary>The player has cleared this mission at least once.</summary>
    Completed,
}
