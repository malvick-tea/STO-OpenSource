namespace Garupan.Client.Ui.Match.Network;

/// <summary>
/// The local player's reading of a finished match. <see cref="NetworkMatchClient"/>
/// derives it from the objective <see cref="Garupan.Sim.Protocol.MatchOverFrame"/> the
/// server broadcasts — the same frame resolves to <see cref="Victory"/> for the winner
/// and <see cref="Defeat"/> for everyone else.
/// </summary>
public enum NetworkMatchVerdict
{
    /// <summary>The local player — or the local player's team — won the match.</summary>
    Victory,

    /// <summary>Another tank or another team won.</summary>
    Defeat,

    /// <summary>Every contender was knocked out; nobody won.</summary>
    Draw,
}
