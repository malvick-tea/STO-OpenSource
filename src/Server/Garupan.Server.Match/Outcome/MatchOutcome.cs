using Garupan.Sim.Components;

namespace Garupan.Server.Match.Outcome;

/// <summary>
/// The result of a match as <see cref="MatchOutcomeTracker"/> reports it. A plain value —
/// the tracker latches one of these, the host reads it to freeze the match, and a later
/// phase broadcasts it to clients as a match-over frame.
/// </summary>
/// <param name="Kind">Whether the match is still running, won, or drawn.</param>
/// <param name="WinnerNetworkId">The winning tank's network id under
/// <see cref="MatchOutcomeRule.LastTankStanding"/>; <c>0</c> otherwise.</param>
/// <param name="WinnerTeam">The winning team under
/// <see cref="MatchOutcomeRule.LastTeamStanding"/>; <see cref="Team.None"/> otherwise.</param>
public readonly record struct MatchOutcome(MatchOutcomeKind Kind, uint WinnerNetworkId, Team WinnerTeam)
{
    /// <summary>The match is still being contested — the tracker's pre-decision state.</summary>
    public static MatchOutcome InProgress => new(MatchOutcomeKind.InProgress, 0u, Team.None);

    /// <summary>The match ended with every contender knocked out — no winner.</summary>
    public static MatchOutcome Draw => new(MatchOutcomeKind.Draw, 0u, Team.None);

    /// <summary>True once the match has been decided — a winner or a draw.</summary>
    public bool IsDecided => Kind != MatchOutcomeKind.InProgress;

    /// <summary>A single tank won the free-for-all, identified by its network id.</summary>
    public static MatchOutcome TankWinner(uint networkId) =>
        new(MatchOutcomeKind.Winner, networkId, Team.None);

    /// <summary>One team won the team match.</summary>
    public static MatchOutcome TeamWinner(Team team) =>
        new(MatchOutcomeKind.Winner, 0u, team);
}
