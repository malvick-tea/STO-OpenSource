using System.Collections.Generic;
using Arch.Core;
using Garupan.Sim.Components;
using Opus.Foundation;

namespace Garupan.Sim.Snapshot;

/// <summary>
/// Walks the live ECS world once and emits a frozen <see cref="WorldSnapshot"/>. Pure
/// read — never mutates the registry.
///
/// Capture contract:
/// <list type="bullet">
/// <item><description>Tanks are entities with the (<see cref="Transform"/>, <see cref="Turret"/>, <see cref="Hull"/>)
///     triple. A projectile has Transform but no Turret/Hull, so it falls through to the
///     projectile section without ambiguity. <see cref="KnockedOut"/> is layered onto the
///     row as <see cref="EntityStateFlags.KnockedOut"/>; the entity stays in the snapshot.</description></item>
/// <item><description>Projectiles are entities with (<see cref="Projectile"/>, <see cref="Transform"/>).
///     Lifetime is intentionally not echoed (the snapshot is a tick-frozen state, not a
///     plan).</description></item>
/// </list>
///
/// Ported from <c>svo::engine::snapshot::capture</c>. Each tank row is keyed by its
/// <see cref="NetworkId"/> component when the authoritative host has stamped one, falling
/// back to the Arch slot id for single-player / determinism-harness worlds that don't.
/// The C++ version additionally *filters* the query to NetworkId-bearing entities; Garupan
/// defers that filter until client-side prediction introduces entities that must not be
/// replicated — today every captured tank is a replicated one.
/// </summary>
public static class SnapshotCapture
{
    public static WorldSnapshot Capture(World world, Tick tick)
    {
        Ensure.NotNull(world);
        var raw = world.Raw;

        var entities = new List<EntitySnapshot>();
        var tankQuery = new QueryDescription().WithAll<Transform, Turret, Hull>();
        raw.Query(in tankQuery, (Entity e, ref Transform tf, ref Turret turret, ref Hull _) =>
        {
            var flags = EntityStateFlags.None;
            if (raw.Has<KnockedOut>(e))
            {
                flags |= EntityStateFlags.KnockedOut;
            }

            var minBarrelPitch = EntitySnapshot.UnboundedMinBarrelPitchRadians;
            var maxBarrelPitch = EntitySnapshot.UnboundedMaxBarrelPitchRadians;
            if (raw.Has<GunMount>(e))
            {
                var mount = raw.Get<GunMount>(e);
                minBarrelPitch = mount.MinPitchRadians;
                maxBarrelPitch = mount.MaxPitchRadians;
            }

            var gunRecoilTravel = raw.Has<GunRecoilState>(e)
                ? raw.Get<GunRecoilState>(e).TravelMeters
                : 0f;

            entities.Add(new EntitySnapshot(
                Id: ResolveRowId(raw, e),
                Position: tf.Position,
                YawRadians: tf.YawRadians,
                TurretYawRadians: turret.YawRadians,
                StateFlags: flags,
                BarrelPitchRadians: turret.BarrelPitchRadians,
                MinBarrelPitchRadians: minBarrelPitch,
                MaxBarrelPitchRadians: maxBarrelPitch,
                GunRecoilTravelMeters: gunRecoilTravel));
        });

        var projectiles = new List<ProjectileSnapshot>();
        var projectileQuery = new QueryDescription().WithAll<Projectile, Transform>();
        raw.Query(in projectileQuery, (Entity e, ref Projectile proj, ref Transform tf) =>
        {
            projectiles.Add(new ProjectileSnapshot(
                Id: e.Id,
                Position: tf.Position,
                Velocity: proj.Velocity,
                Family: proj.Type,
                VisualHeightMeters: proj.VisualHeightMeters,
                VerticalVelocityMps: proj.VerticalVelocityMps,
                DistanceTravelledMeters: proj.DistanceTravelledMeters,
                LaunchPosition: proj.LaunchPosition,
                LaunchVisualHeightMeters: proj.LaunchVisualHeightMeters,
                OwnerEntityId: ResolveOwnerRowId(raw, e)));
        });

        return new WorldSnapshot(tick, entities, projectiles)
        {
            Props = CaptureFelledProps(raw),
        };
    }

    /// <summary>Collects the props that have left <see cref="Components.PropState.Standing"/> —
    /// the only prop state the client cannot derive from the static map layout it already owns.
    /// Standing props are omitted (the default the client draws), keeping the section empty for
    /// the overwhelming majority of ticks and for the prop-free determinism / replay scenarios.</summary>
    private static IReadOnlyList<PropSnapshot> CaptureFelledProps(Arch.Core.World raw)
    {
        List<PropSnapshot>? felled = null;
        var query = new QueryDescription().WithAll<DestructibleProp>();
        raw.Query(in query, (ref DestructibleProp prop) =>
        {
            if (prop.State == PropState.Standing)
            {
                return;
            }

            felled ??= new List<PropSnapshot>();
            felled.Add(new PropSnapshot(prop.PropId, prop.State, prop.FallYawRadians, prop.StateSeconds));
        });

        return felled ?? (IReadOnlyList<PropSnapshot>)System.Array.Empty<PropSnapshot>();
    }

    /// <summary>The snapshot row id for a tank: its stable <see cref="NetworkId"/> when the
    /// authoritative host has stamped one, otherwise the Arch slot id — the Phase-0
    /// single-player / determinism fallback that keeps golden-hash scenarios unchanged.</summary>
    private static int ResolveRowId(Arch.Core.World raw, Entity e) =>
        raw.Has<NetworkId>(e) ? (int)raw.Get<NetworkId>(e).Value : e.Id;

    private static int ResolveOwnerRowId(Arch.Core.World raw, Entity projectile)
    {
        if (!raw.Has<Owner>(projectile))
        {
            return 0;
        }

        var owner = raw.Get<Owner>(projectile).Entity.Raw;
        return raw.IsAlive(owner) ? ResolveRowId(raw, owner) : 0;
    }
}
