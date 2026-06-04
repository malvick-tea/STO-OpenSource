using System.Collections.Generic;
using Arch.Core;
using Garupan.Sim.Components;

namespace Garupan.Sim.Systems;

/// <summary>
/// Commits buffered player / network intent into the authoritative
/// <see cref="DriveInput"/>, <see cref="TurretTarget"/>, and <see cref="FireIntent"/>
/// components. Runs first in the per-tick schedule so the rest of the systems see a
/// coherent input snapshot for the tick.
///
/// For every entity carrying <see cref="PendingInput"/> (and not <see cref="KnockedOut"/>):
/// <list type="number">
/// <item><description>Clamp throttle / steering to [-1, 1] and write them into DriveInput.</description></item>
/// <item><description>Copy turret yaw into TurretTarget.</description></item>
/// <item><description>If <see cref="InputFlags.Fire"/> is set and FireIntent isn't already attached, attach a one-shot
///     <see cref="FireIntent"/> (GunFire consumes it later in the same tick).</description></item>
/// <item><description>Remove the PendingInput so a stale frame doesn't bleed into the next tick. The
///     next input frame from the same source will re-attach a fresh PendingInput before
///     ApplyInputs runs again.</description></item>
/// </list>
///
/// Knocked-out tanks are filtered by the query: a disabled tank cannot receive new orders.
///
/// Two-phase iteration (collect entities first, mutate after) matches the pattern used by
/// <see cref="ProjectileHitResolveSystem"/> / <see cref="GunFireSystem"/> — Arch chunk
/// pointers can shift when components are added or removed, and snapshotting the entity
/// list up front keeps the loop safe regardless.
///
/// Order: 100 — Input band, runs before AI (200), HullDrive (300), TurretAim (400).
/// Ported from <c>svo/engine/src/systems/apply_inputs.cpp</c>.
/// </summary>
public sealed class ApplyInputsSystem : IFixedSystem
{
    public string Name => "ApplyInputs";

    public int Order => 100;

    public void Tick(in TickContext ctx)
    {
        var world = ctx.World;
        var raw = world.Raw;

        var targets = new List<Entity>();
        var query = new QueryDescription()
            .WithAll<PendingInput>()
            .WithNone<KnockedOut>();
        raw.Query(in query, (Entity e) => targets.Add(e));

        foreach (var rawEntity in targets)
        {
            var handle = new EntityHandle(rawEntity);
            var pending = raw.Get<PendingInput>(rawEntity);

            if (raw.Has<DriveInput>(rawEntity))
            {
                world.Set(handle, new DriveInput
                {
                    Throttle = ClampUnit(pending.Throttle),
                    Steering = ClampUnit(pending.Steering),
                });
            }

            if (raw.Has<TurretTarget>(rawEntity))
            {
                world.Set(handle, new TurretTarget { YawRadians = pending.TurretYawRadians });
            }

            if (raw.Has<Turret>(rawEntity) && raw.Has<GunMount>(rawEntity))
            {
                var turret = raw.Get<Turret>(rawEntity);
                var mount = raw.Get<GunMount>(rawEntity);
                turret.BarrelPitchRadians = System.Math.Clamp(
                    pending.BarrelPitchRadians,
                    mount.MinPitchRadians,
                    mount.MaxPitchRadians);
                world.Set(handle, turret);
            }

            if ((pending.Flags & InputFlags.Fire) != InputFlags.None && !raw.Has<FireIntent>(rawEntity))
            {
                world.Add(handle, default(FireIntent));
            }

            world.Remove<PendingInput>(handle);
        }
    }

    private static float ClampUnit(float v) => v < -1f ? -1f : v > 1f ? 1f : v;
}
