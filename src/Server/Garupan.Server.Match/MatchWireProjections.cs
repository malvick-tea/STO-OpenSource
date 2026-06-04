using Garupan.Server.Match.Outcome;
using Garupan.Sim.Protocol;

namespace Garupan.Server.Match;

/// <summary>Pure projections from the server-tier match model
/// (<see cref="Garupan.Server.Match.Outcome"/>) onto the Sim-tier wire frames
/// (<see cref="Garupan.Sim.Protocol"/>). The single translation boundary between the two
/// tiers — extracted from <see cref="MatchHost"/> so the host stays orchestration-only and
/// the maps are unit-testable in isolation.</summary>
internal static class MatchWireProjections
{
    /// <summary>Maps the host's configured outcome rule onto the mode-kind byte stamped
    /// in every <see cref="WelcomeFrame"/>. <see cref="MatchOutcomeRule.LastTeamStanding"/>
    /// is team-tactical on the wire; every other rule is free-for-all — matching the
    /// local test line-up (Tactical 5v5 ↔ team rule, Hungry Battles ↔ FFA rule).</summary>
    public static WelcomeMatchModeKind ToWelcomeModeKind(MatchOutcomeRule rule) => rule switch
    {
        MatchOutcomeRule.LastTeamStanding => WelcomeMatchModeKind.TeamTactical,
        _ => WelcomeMatchModeKind.FreeForAll,
    };

    /// <summary>Projects a decided server-tier <see cref="MatchOutcome"/> onto the Sim-tier
    /// <see cref="MatchOverFrame"/> the wire carries. Called only for a decided outcome, so
    /// the kind is always Winner or Draw.</summary>
    public static MatchOverFrame ToMatchOverFrame(MatchOutcome outcome) => outcome.Kind switch
    {
        MatchOutcomeKind.Draw => new MatchOverFrame(MatchOverResult.Draw, WinnerNetworkId: 0u, WinnerTeam: 0),
        _ => new MatchOverFrame(
            MatchOverResult.Winner,
            outcome.WinnerNetworkId,
            (byte)outcome.WinnerTeam),
    };
}
