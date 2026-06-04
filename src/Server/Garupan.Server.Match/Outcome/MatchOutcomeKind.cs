namespace Garupan.Server.Match.Outcome;

/// <summary>
/// The three states a match outcome can be in. <see cref="MatchOutcomeTracker"/> latches
/// the first non-<see cref="InProgress"/> value it reaches.
/// </summary>
public enum MatchOutcomeKind
{
    /// <summary>The match is still being contested — no winner yet.</summary>
    InProgress,

    /// <summary>One side won. The winner is identified by
    /// <see cref="MatchOutcome.WinnerNetworkId"/> (last-tank-standing) or
    /// <see cref="MatchOutcome.WinnerTeam"/> (last-team-standing).</summary>
    Winner,

    /// <summary>The match ended with no winner — every contender was knocked out, so
    /// nobody was left standing.</summary>
    Draw,
}
