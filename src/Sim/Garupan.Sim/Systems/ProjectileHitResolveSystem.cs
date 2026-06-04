using System;
using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Garupan.Sim.Components;

namespace Garupan.Sim.Systems;

/// <summary>
/// Resolves projectile-vs-tank impacts. Two-phase: first collect candidate projectiles
/// + targets into snapshots, then run the n×m hit test. Snapshotting first sidesteps
/// the question of whether Arch handles registry mutation mid-iteration; per-tick
/// allocations are negligible for Phase-0 fire rates.
///
/// Hit model: closest approach along the integrated segment must enter the target
/// radius while the interpolated projectile height intersects its silhouette. This
/// catches fast rounds that cross an entire vehicle between fixed ticks.
///
/// Penetration model: classify the impact sector (Front / Side / Rear) from the hull-local
/// impact azimuth and the hull-vs-turret band from the impact height. The struck plate's slope
/// plus the shot's azimuth give an impact obliquity (<see cref="ArmorPenetrationResolver"/>),
/// from which the effective line-of-sight thickness follows; the round's penetration is sampled
/// from its <see cref="PenetrationProfile"/> at the impact range. penetration ≥ line-of-sight
/// → KnockedOut. The mantlet and roof plates are carried in <see cref="ArmorMap"/> for a future
/// gun-azimuth / plunging-fire refinement; the 2D sector model resolves frontal turret hits
/// against the turret face.
///
/// Order: 600 — runs immediately after integrate.
/// Ported from <c>svo/engine/src/systems/projectile_hit_resolve.cpp</c>.
/// </summary>
public sealed class ProjectileHitResolveSystem : IFixedSystem
{
    /// <summary>Fallback silhouette height (m) for an entity spawned without a per-tank
    /// <see cref="Silhouette"/> (test fixtures, debug spawns). Real tanks carry their own
    /// <c>body_height</c>-derived value, stamped by <see cref="Spawn.ChassisBuilder.Silhouette"/>.</summary>
    public const float TargetSilhouetteHeightMeters = 3f;

    /// <summary>Fallback hull/turret split height (m), above which an impact resolves against the
    /// turret band, for an entity without a per-tank <see cref="Silhouette"/>. ~1.5 m is the legacy
    /// the medium tank-class value; real tanks scale it from their own <c>body_height</c>.</summary>
    public const float HullTurretSplitHeightMeters = 1.5f;

    private enum Sector
    {
        Front,
        Side,
        Rear,
    }

    public string Name => "ProjectileHitResolve";

    public int Order => 600;

    public void Tick(in TickContext ctx)
    {
        var world = ctx.World.Raw;
        var commands = ctx.Commands;
        var projectiles = new List<Entity>();
        var targets = new List<Entity>();

        var pq = new QueryDescription().WithAll<Transform, Projectile>();
        world.Query(in pq, (Entity e, ref Transform _, ref Projectile _) =>
        {
            if (world.Has<Dead>(e))
            {
                return;
            }

            projectiles.Add(e);
        });

        var tq = new QueryDescription().WithAll<Transform, Hull, HitRadius>();
        world.Query(in tq, (Entity e, ref Transform _, ref Hull _, ref HitRadius _) =>
        {
            // Already-dead-or-out targets are not legal hit candidates. A respawn-shielded
            // tank is treated as intangible for the duration of its window — projectiles
            // pass cleanly through (no KnockedOut, no projectile consumption) so a
            // spawn-camper at the anchor cannot one-shot the returning crew.
            if (world.Has<Dead>(e) || world.Has<KnockedOut>(e) || world.Has<RespawnInvulnerable>(e))
            {
                return;
            }

            targets.Add(e);
        });

        foreach (var projectile in projectiles)
        {
            ref var projTf = ref world.Get<Transform>(projectile);
            ref var projData = ref world.Get<Projectile>(projectile);

            // Owner is optional — projectile entities created outside gun_fire (test
            // fixtures, debug spawns) may omit it. Treat "no owner" as "no skip target".
            var ownerEntity = Entity.Null;
            if (world.Has<Owner>(projectile))
            {
                ownerEntity = world.Get<Owner>(projectile).Entity.Raw;
            }

            foreach (var target in targets)
            {
                if (target.Equals(ownerEntity))
                {
                    continue;
                }

                // Skip targets that an earlier projectile in this same pass has already
                // knocked out — they are still in the snapshot but the tag now excludes them.
                if (world.Has<KnockedOut>(target))
                {
                    continue;
                }

                ref var targetTf = ref world.Get<Transform>(target);
                ref var targetHull = ref world.Get<Hull>(target);
                ref var targetRadius = ref world.Get<HitRadius>(target);
                var (silhouetteHeight, hullTurretSplit) = SilhouetteOf(world, target);

                var offset = ClosestImpactOffset(projTf.Position, projData, targetTf.Position, out var segmentT);
                var distSq = (offset.X * offset.X) + (offset.Y * offset.Y);
                var r = targetRadius.Meters;
                var impactHeight = InterpolatedHeight(in projData, segmentT);
                if (distSq > r * r || impactHeight < 0f || impactHeight > silhouetteHeight)
                {
                    continue; // miss
                }

                if (Defeats(in projData, in targetTf, in targetHull.Armor, offset, impactHeight, hullTurretSplit)
                    && !world.Has<KnockedOut>(target))
                {
                    // Penetration. armored combat-canonical white flag pops; in the historical record a
                    // defeating round usually takes the tank out in one go.
                    world.Add(target, default(KnockedOut));
                }

                // else: bounce. Plate held; no state change on the target.

                // Projectile is consumed by the impact regardless of penetration.
                if (!world.Has<Dead>(projectile))
                {
                    world.Add(projectile, default(Dead));
                }

                break;
            }

            if (projData.HitGround && !world.Has<Dead>(projectile))
            {
                world.Add(projectile, default(Dead));
            }
        }

        _ = commands; // CommandBuffer integration arrives once SystemPipeline routes sets through it.
    }

    private static Vector2 ClosestImpactOffset(
        Vector2 position,
        Projectile projectile,
        Vector2 targetPosition,
        out float segmentT)
    {
        if (!projectile.HasIntegratedSegment)
        {
            segmentT = 1f;
            return position - targetPosition;
        }

        var segment = position - projectile.PreviousPosition;
        var lengthSquared = segment.LengthSquared();
        segmentT = lengthSquared <= float.Epsilon
            ? 1f
            : Math.Clamp(Vector2.Dot(targetPosition - projectile.PreviousPosition, segment) / lengthSquared, 0f, 1f);
        return projectile.PreviousPosition + (segment * segmentT) - targetPosition;
    }

    private static float InterpolatedHeight(in Projectile projectile, float segmentT) =>
        projectile.PreviousVisualHeightMeters
            + ((projectile.VisualHeightMeters - projectile.PreviousVisualHeightMeters) * segmentT);

    /// <summary>Per-tank vertical hit geometry from the struck entity's <see cref="Silhouette"/>,
    /// or the legacy flat fallback for entities (test fixtures, debug spawns) spawned without one.</summary>
    private static (float Height, float HullTurretSplit) SilhouetteOf(Arch.Core.World world, Entity target)
    {
        if (!world.Has<Silhouette>(target))
        {
            return (TargetSilhouetteHeightMeters, HullTurretSplitHeightMeters);
        }

        var silhouette = world.Get<Silhouette>(target);
        return (silhouette.HeightMeters, silhouette.HullTurretSplitMeters);
    }

    /// <summary>
    /// Resolves whether the round defeats the struck plate: picks the sector + hull/turret band,
    /// derives the plate's line-of-sight thickness from its slope and the shot's obliquity, and
    /// compares it against the round's penetration sampled at the impact range.
    /// </summary>
    private static bool Defeats(
        in Projectile projectile,
        in Transform targetTf,
        in ArmorMap armor,
        Vector2 impactOffsetWorld,
        float impactHeight,
        float hullTurretSplitMeters)
    {
        var (sector, localY) = ClassifySector(impactOffsetWorld, targetTf.YawRadians);
        var turret = impactHeight > hullTurretSplitMeters;
        var (thicknessMm, slopeDegrees) = PlateOf(in armor, sector, turret);
        var normal = PlateOutwardNormal(sector, targetTf.YawRadians, localY);
        var obliquity = ArmorPenetrationResolver.ObliquityDegrees(slopeDegrees, projectile.Velocity, normal);
        var lineOfSightMm = ArmorPenetrationResolver.EffectiveThicknessMm(thicknessMm, obliquity);

        var impactWorld = targetTf.Position + impactOffsetWorld;
        var rangeMeters = Vector2.Distance(projectile.LaunchPosition, impactWorld);
        return projectile.Penetration.NormalPenetrationAt(rangeMeters) >= lineOfSightMm;
    }

    /// <summary>
    /// Project the world-frame "target→impact" vector onto the target's hull-local axes and pick a
    /// 90°-quadrant sector. +X local = hull forward; +Y local = hull left. The local lateral
    /// coordinate is returned too, so the caller can resolve which side plate faces the shot.
    /// </summary>
    private static (Sector Sector, float LocalY) ClassifySector(Vector2 impactOffsetWorld, float hullYawWorld)
    {
        var cosY = MathF.Cos(hullYawWorld);
        var sinY = MathF.Sin(hullYawWorld);

        // World → hull-local rotation by -hull_yaw. Spelled out so the sign convention is obvious.
        var localX = (cosY * impactOffsetWorld.X) + (sinY * impactOffsetWorld.Y);
        var localY = (-sinY * impactOffsetWorld.X) + (cosY * impactOffsetWorld.Y);

        if (MathF.Abs(localX) > MathF.Abs(localY))
        {
            return (localX > 0f ? Sector.Front : Sector.Rear, localY);
        }

        return (Sector.Side, localY);
    }

    /// <summary>Picks the plate thickness + mounting slope for the sector and hull/turret band.</summary>
    private static (float ThicknessMm, float SlopeDegrees) PlateOf(in ArmorMap armor, Sector sector, bool turret) =>
        (sector, turret) switch
        {
            (Sector.Front, false) => (armor.HullFrontMm, armor.HullFrontSlopeDeg),
            (Sector.Side, false) => (armor.HullSideMm, armor.HullSideSlopeDeg),
            (Sector.Rear, false) => (armor.HullRearMm, armor.HullRearSlopeDeg),
            (Sector.Front, true) => (armor.TurretFrontMm, armor.TurretFrontSlopeDeg),
            (Sector.Side, true) => (armor.TurretSideMm, armor.TurretSideSlopeDeg),
            _ => (armor.TurretRearMm, armor.TurretRearSlopeDeg),
        };

    /// <summary>Outward horizontal normal of the struck plate in world space — hull forward for the
    /// front, its negation for the rear, and the hull lateral axis on the impact's side.</summary>
    private static Vector2 PlateOutwardNormal(Sector sector, float hullYawWorld, float localY)
    {
        var forward = new Vector2(MathF.Cos(hullYawWorld), MathF.Sin(hullYawWorld));
        return sector switch
        {
            Sector.Front => forward,
            Sector.Rear => -forward,
            _ => localY >= 0f
                ? new Vector2(-MathF.Sin(hullYawWorld), MathF.Cos(hullYawWorld))
                : new Vector2(MathF.Sin(hullYawWorld), -MathF.Cos(hullYawWorld)),
        };
    }
}
