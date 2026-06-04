using System.Numerics;
using Garupan.Sim.Components;

namespace Garupan.Sim.Snapshot;

/// <summary>
/// State of one in-flight projectile at a single tick. Independent of <see cref="EntitySnapshot"/>
/// so the projectile section of the snapshot can evolve at its own pace.
///
/// <see cref="Velocity"/> lets renderers orient the shell along its flight path and lets
/// a future client-side extrapolator or replay-frame interpolator key
/// off the exact ballistic state at the snapshot boundary, and the cost (8 bytes per
/// row) is irrelevant compared to the storage of the rest of the row.
///
/// Lifetime is intentionally absent — the consumer treats each appearance as transient
/// and drops the row the tick the projectile despawns; the simulation already owns
/// lifetime arithmetic via <see cref="Systems.LifetimeDecaySystem"/>.
///
/// Ported from <c>svo::protocol::ProjectileSnapshot</c>. The C++ version mirrors
/// <c>AmmoType</c> into a separate <c>ProjectileFamily</c> enum to keep
/// <c>svo::protocol</c> free of a layering dependency on <c>svo::shared</c>; Garupan.Sim
/// has no such firewall (the Snapshot namespace lives in the same assembly as the
/// components), so we reuse <see cref="AmmoType"/> directly.
/// </summary>
public readonly record struct ProjectileSnapshot(
    int Id,
    Vector2 Position,
    Vector2 Velocity,
    AmmoType Family,
    float VisualHeightMeters = 0f,
    float VerticalVelocityMps = 0f,
    float DistanceTravelledMeters = 0f,
    Vector2 LaunchPosition = default,
    float LaunchVisualHeightMeters = 0f,
    int OwnerEntityId = 0);
