namespace Garupan.Sim.Protocol;

/// <summary>
/// The terminal verdict carried by a <see cref="MatchOverFrame"/>. A match-over frame is
/// only ever sent for a decided match, so there is no "in progress" member — the absence
/// of the frame is the in-progress state.
/// <para>The numeric values are pinned: the discriminant travels on the wire as a
/// <see cref="uint"/>.</para>
/// </summary>
public enum MatchOverResult : uint
{
    /// <summary>One side won. <see cref="MatchOverFrame.WinnerNetworkId"/> /
    /// <see cref="MatchOverFrame.WinnerTeam"/> name the winner.</summary>
    Winner = 0,

    /// <summary>The match ended with every contender knocked out — no winner.</summary>
    Draw = 1,
}
