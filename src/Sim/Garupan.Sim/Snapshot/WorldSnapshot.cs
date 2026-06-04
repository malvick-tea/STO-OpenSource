using System.Collections.Generic;
using Opus.Foundation;

namespace Garupan.Sim.Snapshot;

/// <summary>
/// Authoritative state of every replicable entity in the world at a given tick.
/// Produced by <see cref="SnapshotCapture.Capture"/>; consumed by replay tools,
/// match-end summary screens, golden-hash determinism tests, and (eventually) the
/// network layer's wire codec.
///
/// Parallel lists rather than one heterogenous list so consumers can iterate the
/// section they care about without runtime type-dispatch. The capture order is the
/// order entities appear in the underlying ECS view — stable within a single capture,
/// not guaranteed across captures (Arch can reshuffle as archetypes change).
///
/// Ported from <c>svo::protocol::WorldSnapshot</c>.
/// </summary>
public sealed record WorldSnapshot(
    Tick Tick,
    IReadOnlyList<EntitySnapshot> Entities,
    IReadOnlyList<ProjectileSnapshot> Projectiles)
{
    /// <summary>Destructible props that have left <see cref="Components.PropState.Standing"/> this
    /// round — felled poles, toppling signs, shattered clutter — keyed by stable prop id. Only the
    /// non-standing set travels; the client renders every other prop standing from the static map
    /// layout. Init-only with an empty default so the three-argument capture stays a positional
    /// construction for the determinism harness and replay tooling, which carry no props.</summary>
    public IReadOnlyList<PropSnapshot> Props { get; init; } = System.Array.Empty<PropSnapshot>();
}
