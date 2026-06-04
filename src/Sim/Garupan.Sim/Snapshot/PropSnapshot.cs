using Garupan.Sim.Components;

namespace Garupan.Sim.Snapshot;

/// <summary>
/// Replicated state of one destructible map prop that is no longer standing. The snapshot
/// carries <em>only</em> props that have left <see cref="PropState.Standing"/> — a felled
/// pole, a toppling sign, a shattered bin — keyed by the prop's stable
/// <see cref="DestructibleProp.PropId"/>. The client owns the full static prop layout (loaded
/// from the same map <c>-props.csv</c> the server spawned from) and draws every prop standing by
/// default; a row here authoritatively overrides one prop to its felled pose so the player sees
/// it break exactly as the simulation resolved it — without the prop geometry ever crossing the
/// wire.
/// </summary>
/// <remarks>
/// <see cref="ToppleSeconds"/> is the prop's age in its current transient state (see
/// <see cref="DestructibleProp.StateSeconds"/>); the client hinges a <see cref="PropState.Toppling"/>
/// member by that fraction of the topple duration, so the fall is driven by the authoritative clock
/// rather than a client-side timer that would skew on a late join. A <see cref="PropState.Broken"/>
/// row tells the client to hide the prop outright. The set is monotonic within a round and clears on
/// the next Welcome, so a prop absent from the section is — by definition — still standing.
/// </remarks>
public readonly record struct PropSnapshot(
    int PropId,
    PropState State,
    float FallYawRadians,
    float ToppleSeconds);
