using System;
using System.Numerics;
using Garupan.Content;
using Garupan.Server.Match.Outcome;
using Garupan.Sim.Components;
using Opus.Foundation;

namespace Garupan.Server.Match;

/// <summary>
/// Maps a <see cref="MatchMode"/> (from the local test catalogue) onto a concrete
/// <see cref="MatchHostOptions"/>. Pure; no transport or session dependency, so the
/// arithmetic is unit-testable directly.
/// </summary>
/// <remarks>
/// The local test line-up ([[garupan-local test-2026]]):
/// <list type="bullet">
/// <item><description>Hungry Battles — <see cref="MatchModeKind.FreeForAll"/>, 10v10 free-for-all,
/// last alive wins; mode CSV sets <see cref="MatchMode.RespawnLimit"/> = 3.
/// Mapped to <see cref="MatchOutcomeRule.LastTankStanding"/>.</description></item>
/// <item><description>Tactical 5v5 — <see cref="MatchModeKind.TeamTactical"/>, two opposing teams,
/// last team standing wins; mode CSV sets <see cref="MatchMode.RespawnLimit"/> = 1.
/// Mapped to <see cref="MatchOutcomeRule.LastTeamStanding"/>; per-peer team assignment
/// runs through <see cref="TeamAssignment"/>.</description></item>
/// </list>
/// </remarks>
public static class MatchHostOptionsFactory
{
    /// <summary>Builds match-host options that implement <paramref name="mode"/>.
    /// Everything else (tick rate, snapshot cadence, spawn anchor) is supplied by the
    /// caller — the server console plumbs them from its CLI surface; tests pass canonical
    /// values.</summary>
    public static MatchHostOptions ForMode(
        MatchMode mode,
        TankSpec playerSpec,
        Vector2 spawnAnchor,
        int tickRateHz = GameTime.DefaultTickRateHz,
        int snapshotIntervalTicks = 1)
    {
        ArgumentNullException.ThrowIfNull(mode);
        ArgumentNullException.ThrowIfNull(playerSpec);

        var outcomeRule = mode.Kind switch
        {
            MatchModeKind.TeamTactical => MatchOutcomeRule.LastTeamStanding,
            MatchModeKind.FreeForAll => MatchOutcomeRule.LastTankStanding,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), $"Unhandled MatchModeKind {mode.Kind}"),
        };

        var respawns = checked((byte)Math.Clamp(mode.RespawnLimit, 0, byte.MaxValue));

        return new MatchHostOptions(
            PlayerSpec: playerSpec,
            PlayerTeam: Team.PlayerSchool,
            SpawnAnchor: spawnAnchor,
            TickRateHz: tickRateHz,
            SnapshotIntervalTicks: snapshotIntervalTicks,
            OutcomeRule: outcomeRule,
            RespawnsPerPeer: respawns);
    }
}
