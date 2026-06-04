using System.Numerics;

namespace Garupan.Sim.Snapshot;

/// <summary>
/// State of one replicated entity (tank) at a single tick. Frozen, read-only view
/// derived from the live ECS — never mutated after capture.
///
/// Carries everything a downstream consumer (replay scrubber, mid-match dialogue
/// trigger evaluator, future wire codec, golden-hash determinism test) needs to:
/// <list type="bullet">
/// <item><description>identify the entity by <see cref="Id"/> (Phase-0 = Arch slot, swaps to a stable
///     NetworkId field when the multiplayer layer lands);</description></item>
/// <item><description>reconstruct its world pose (<see cref="Position"/> + <see cref="YawRadians"/>);</description></item>
/// <item><description>render the turret independently (<see cref="TurretYawRadians"/>);</description></item>
/// <item><description>render barrel elevation (<see cref="BarrelPitchRadians"/>);</description></item>
/// <item><description>clamp local gun preview to this chassis' installation limits
///     (<see cref="MinBarrelPitchRadians"/> + <see cref="MaxBarrelPitchRadians"/>);</description></item>
/// <item><description>react to lifecycle state (<see cref="StateFlags"/>).</description></item>
/// </list>
///
/// Velocity is intentionally absent: motion is reconstructed from inter-tick position
/// deltas. The hit-point pool is also absent — STO models knock-outs as a binary
/// penetration outcome (see <see cref="Systems.ProjectileHitResolveSystem"/>), so
/// "alive vs out" is the single <see cref="EntityStateFlags.KnockedOut"/> bit.
///
/// Ported from <c>svo::protocol::EntitySnapshot</c>.
/// </summary>
public readonly record struct EntitySnapshot(
    int Id,
    Vector2 Position,
    float YawRadians,
    float TurretYawRadians,
    EntityStateFlags StateFlags,
    float BarrelPitchRadians = 0f,
    float MinBarrelPitchRadians = -1.5707964f,
    float MaxBarrelPitchRadians = 1.5707964f,
    float GunRecoilTravelMeters = 0f)
{
    /// <summary>Neutral fallback for hand-built rows that predate per-chassis gun mounts.</summary>
    public const float UnboundedMinBarrelPitchRadians = -1.5707964f;

    /// <summary>Neutral fallback for hand-built rows that predate per-chassis gun mounts.</summary>
    public const float UnboundedMaxBarrelPitchRadians = 1.5707964f;
}
