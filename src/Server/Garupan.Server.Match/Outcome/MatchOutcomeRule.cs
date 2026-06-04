namespace Garupan.Server.Match.Outcome;

/// <summary>
/// How a <see cref="MatchOutcomeTracker"/> decides a match is over. One value per
/// local test match mode.
/// </summary>
public enum MatchOutcomeRule
{
    /// <summary>Free-for-all: the match ends when a single tank is the last one not
    /// knocked out — or when none remain, a mutual knock-out draw. Team affiliation is
    /// ignored. The local test "Hungry Battles" mode.</summary>
    LastTankStanding,

    /// <summary>Team match: the match ends when every still-fighting tank belongs to one
    /// team and the rest are wiped — or when all teams are wiped, a draw. Requires the
    /// roster to contain two or more teams. The local test "Tactical 5v5" mode.</summary>
    LastTeamStanding,
}
