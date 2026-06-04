using System;
using System.Collections.Generic;
using Garupan.Sim.Systems;

namespace Garupan.Sim.Loop;

/// <summary>
/// Builds the canonical Phase-0 system pipeline for a match. Sim-tier composition: lives
/// next to <see cref="FixedStepLoop"/> so both client-side <c>MatchSession</c> and
/// server-side <c>Garupan.Server.Match.MatchHost</c> share one schedule of record. Moving
/// the catalogue here also kills the prior cross-layer reference (Client.Ui owning a
/// pipeline factory that <i>only</i> consumed Sim.Systems was an architectural smell —
/// Server has no business depending on Client.Ui to start a match).
///
/// Order bands documented on <see cref="ISystem.Order"/>:
/// <list type="bullet">
/// <item><description>100 — input apply</description></item>
/// <item><description>200 — AI</description></item>
/// <item><description>300 — mobility (hull drive)</description></item>
/// <item><description>340 — obstacle collision (tanks vs impassable buildings)</description></item>
/// <item><description>350 — prop collision (tanks vs destructible map clutter)</description></item>
/// <item><description>400 — aim (turret)</description></item>
/// <item><description>440 — gun recoil return-to-battery</description></item>
/// <item><description>450 — reload tick</description></item>
/// <item><description>460 — projectile integrate</description></item>
/// <item><description>470 — gun fire</description></item>
/// <item><description>600 — hit resolve</description></item>
/// <item><description>650 — respawn</description></item>
/// <item><description>660 — spawn-invulnerability decay</description></item>
/// <item><description>700 — lifetime decay</description></item>
/// <item><description>900 — cleanup dead</description></item>
/// </list>
/// </summary>
public static class MatchPipelineFactory
{
    public static SystemPipeline BuildPhase0() =>
        Build(RespawnSystem.DefaultRespawnDelayTicks, SpawnInvulnerabilitySystem.DefaultInvulnerabilityTicks);

    /// <summary>Builds the pipeline with caller-supplied respawn timing. Single-life
    /// callers can pass <c>0</c> for the delay — the respawn system stays in the
    /// pipeline but is a no-op for any tank without a <see cref="Components.RespawnLives"/>
    /// component. Zero invulnerability ticks opts the respawned tank out of the
    /// shielded window (determinism scenarios, single-player canon missions).
    /// <paramref name="terrainHeightSampler"/> couples the hull dynamics to the DEM terrain surface
    /// (slope force); null is flat ground, so determinism scenarios stay byte-identical. Felled-prop
    /// drive-over is a render-only effect — the 2-D sim does not model it.</summary>
    public static SystemPipeline Build(
        ushort respawnDelayTicks,
        ushort spawnInvulnerabilityTicks,
        Func<float, float, float>? terrainHeightSampler = null)
    {
        var systems = new List<ISystem>
        {
            new ApplyInputsSystem(),
            new AiBotSystem(),
            new HullDriveSystem(terrainHeightSampler),
            new ObstacleCollisionSystem(),
            new PropCollisionSystem(),
            new TurretAimSystem(),
            new GunRecoilTickSystem(),
            new ReloadTickSystem(),
            new ProjectileIntegrateSystem(),
            new GunFireSystem(),
            new ProjectileHitResolveSystem(),
            new RespawnSystem(respawnDelayTicks, spawnInvulnerabilityTicks),
            new SpawnInvulnerabilitySystem(),
            new LifetimeDecaySystem(),
            new CleanupDeadSystem(),
        };
        return new SystemPipeline(systems);
    }
}
