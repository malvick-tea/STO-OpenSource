using Garupan.Sim.Components;

namespace Garupan.Server.Match.Outcome;

/// <summary>
/// One tank's standing as <see cref="MatchOutcomeTracker"/> sees it. The match host
/// derives this each tick from the authoritative world; keeping the tracker's input a
/// plain value list — not the ECS world — makes the outcome rule arithmetic pure and
/// headless-testable.
/// </summary>
/// <param name="NetworkId">Server-assigned replication id of the tank (see
/// <see cref="Garupan.Sim.Components.NetworkId"/>). Identifies the winner under
/// <see cref="MatchOutcomeRule.LastTankStanding"/>.</param>
/// <param name="Team">The tank's team affiliation. Ignored by
/// <see cref="MatchOutcomeRule.LastTankStanding"/>; load-bearing for
/// <see cref="MatchOutcomeRule.LastTeamStanding"/>.</param>
/// <param name="IsKnockedOut">True once the tank carries the armored combat� white flag — on the
/// field but out of the fight.</param>
public readonly record struct MatchParticipant(uint NetworkId, Team Team, bool IsKnockedOut);
