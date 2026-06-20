using System;
using System.Numerics;
using Garupan.Content;
using Garupan.Server.Match.Outcome;
using Garupan.Sim.Components;
using Garupan.Sim.Replay;
using Garupan.Sim.Systems;
using Opus.Foundation;

namespace Garupan.Server.Match;

/// <summary>Configuration for one <see cref="MatchHost"/> instance — what tick rate the
/// authoritative sim runs at, how often snapshots fan out to clients, and the spawn
/// template used for every peer that joins.
/// <para>
/// The spawn template is intentionally minimal for Phase 32: every peer gets the same
/// <see cref="PlayerSpec"/> at a deterministic position derived from the connection
/// order. Heterogeneous spawns (per-school chassis, per-mission start lines) ride on
/// top of this in later phases.
/// </para></summary>
/// <param name="PlayerSpec">Static description of the tank every peer receives at
/// spawn. Re-used across peers — the same record is fine, the Sim reads it as data.</param>
/// <param name="PlayerTeam">The team stamped on every peer in a free-for-all match
/// (<see cref="MatchOutcomeRule.LastTankStanding"/>). A team match
/// (<see cref="MatchOutcomeRule.LastTeamStanding"/>) balances peers across
/// <see cref="Team.PlayerSchool"/> / <see cref="Team.OpponentSchool"/> via
/// <see cref="TeamAssignment"/> instead, so this value is unused there.</param>
/// <param name="SpawnAnchor">First peer spawns here. Subsequent peers spawn at evenly
/// spaced offsets in the +X direction so neither test geometry nor the snapshot trace
/// ends up ambiguous.</param>
/// <param name="SpawnSpacingMeters">Stride between successive spawn slots.</param>
/// <param name="TickRateHz">Authoritative tick rate the <c>FixedStepLoop</c> runs at.</param>
/// <param name="SnapshotIntervalTicks">Broadcast a <c>WorldSnapshot</c> every N ticks.
/// <c>1</c> = every tick (highest fidelity, highest bandwidth); runtime multiplayer
/// typically lands at 3–5 (20–30 Hz snapshot on a 60 Hz tick). Phase 32 default = 1 so
/// integration tests don't need to spin extra ticks waiting for a broadcast.</param>
/// <param name="OutcomeRule">Which match-end condition the host's
/// <see cref="MatchOutcomeTracker"/> watches for. Default
/// <see cref="MatchOutcomeRule.LastTankStanding"/> — the free-for-all rule that matches
/// the current single-team spawn template; <see cref="MatchOutcomeRule.LastTeamStanding"/>
/// becomes meaningful once per-peer team assignment ships.</param>
/// <param name="RespawnsPerPeer">Number of respawns each peer receives at spawn (the
/// initial life is implicit — <c>0</c> means single-life, <c>3</c> means three respawns
/// on top of the initial spawn = four lives total). Hungry Battles defaults set this to
/// 3; Tactical to 1; the Phase-0 single-mode default is 0 (legacy behaviour: a knock-out
/// is permanent). Drives the <see cref="Components.RespawnLives"/> stamp at spawn time.</param>
/// <param name="RespawnDelayTicks">Delay between knock-out and respawn, in sim ticks.
/// Forwarded to <see cref="RespawnSystem"/> through the pipeline factory. Default
/// <see cref="RespawnSystem.DefaultRespawnDelayTicks"/> = 60 (two seconds at 30 Hz).</param>
/// <param name="PostMatchHoldTicks">How long the host stays frozen on a decided match
/// before automatically recycling to the next round. <c>0</c> disables auto-reset — the
/// match freezes for the host process's lifetime, matching the legacy Phase-38 behaviour.
/// Default <see cref="MatchHostDefaults.PostMatchHoldTicks"/> = 150 (five seconds at
/// 30 Hz) — long enough for the client to display the verdict, short enough to keep a
/// busy server rotating through matches.</param>
/// <param name="OpponentSpawnAnchor">Where <see cref="Team.OpponentSchool"/> peers begin
/// in a team match (<see cref="MatchOutcomeRule.LastTeamStanding"/>). <c>null</c> falls
/// back to <see cref="SpawnAnchor"/> shifted by
/// <see cref="MatchHostSpawnPlanner.DefaultTeamSeparationMeters"/> on +X — sane
/// local test default that lets a 5v5 spread without spawn-overlap. Free-for-all
/// matches ignore this; every peer there uses <see cref="SpawnAnchor"/>.</param>
/// <param name="SpawnInvulnerabilityTicks">Length of the post-respawn shielded window
/// in sim ticks. Forwarded to <see cref="RespawnSystem"/> through the pipeline factory;
/// <see cref="ProjectileHitResolveSystem"/> excludes shielded tanks from hit candidates
/// for the window's duration. <c>0</c> disables the shield entirely (determinism
/// scenarios, single-player canon missions). Default
/// <see cref="SpawnInvulnerabilitySystem.DefaultInvulnerabilityTicks"/> = 60 (two
/// seconds at 30 Hz) — long enough for a returning crew to roll off the spawn anchor.</param>
public sealed record MatchHostOptions(
    TankSpec PlayerSpec,
    Team PlayerTeam,
    Vector2 SpawnAnchor,
    float SpawnSpacingMeters = 4.0f,
    int TickRateHz = GameTime.DefaultTickRateHz,
    int SnapshotIntervalTicks = 1,
    MatchOutcomeRule OutcomeRule = MatchOutcomeRule.LastTankStanding,
    byte RespawnsPerPeer = 0,
    ushort RespawnDelayTicks = RespawnSystem.DefaultRespawnDelayTicks,
    int PostMatchHoldTicks = MatchHostDefaults.PostMatchHoldTicks,
    Vector2? OpponentSpawnAnchor = null,
    ushort SpawnInvulnerabilityTicks = SpawnInvulnerabilitySystem.DefaultInvulnerabilityTicks)
{
    public int MaxPlayers { get; init; } = 20;

    public bool AllowLateJoin { get; init; }

    public float VisibilityRadiusMeters { get; init; } = 250f;

    /// <summary>Optional terrain height sampler — world (x east, z north) → surface height. When
    /// set, the authoritative hull dynamics resolve the slope so peers seat on, slide down, and
    /// grip the map relief; the client renders the same field, so server physics and client visuals
    /// agree. Null (default) is flat ground — the legacy behaviour and what every test relies on.
    /// Held off the positional constructor so the record's value identity stays data-only.</summary>
    public Func<float, float, float>? TerrainHeightSampler { get; init; }

    /// <summary>Destructible map props (trees, signs, bins, …) spawned once into the authoritative
    /// world at construction and restored to standing each round. Empty (default) means a clutter-free
    /// arena — the legacy behaviour every test relies on. Held off the positional constructor so the
    /// record's value identity stays data-only.</summary>
    public System.Collections.Generic.IReadOnlyList<MapProp> MapProps { get; init; } =
        System.Array.Empty<MapProp>();

    /// <summary>Impassable static obstacles (building footprints, walls, piers) spawned once into the
    /// authoritative world at construction. Tanks are blocked by these and can never destroy them, so
    /// — unlike props — they need no per-round restore. Empty (default) means an open arena, the
    /// legacy behaviour every test relies on. Held off the positional constructor so the record's
    /// value identity stays data-only.</summary>
    public System.Collections.Generic.IReadOnlyList<MapObstacle> MapObstacles { get; init; } =
        System.Array.Empty<MapObstacle>();

    /// <summary>Optional replay sink. When set, the host records every
    /// snapshot it broadcasts into the sink and flushes it on match-decided
    /// (or on dispose). When null (default), the host uses
    /// <see cref="NullReplaySink"/> so the tick loop has no null-check on
    /// the hot path. Concrete implementations should derive an HKDF
    /// sub-key scoped to the replay domain from the install key — see
    /// <see cref="MatchReplayRecorder"/> for the canonical disk-backed
    /// implementation.</summary>
    public IReplaySink? ReplaySink { get; init; }
}
