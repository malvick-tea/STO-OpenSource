namespace Garupan.Sim.Components;

/// <summary>
/// Stable, server-assigned replication identity for an entity that appears in a
/// <see cref="Garupan.Sim.Snapshot.WorldSnapshot"/>. Unlike the Arch entity slot id — which
/// is recycled the moment an entity is destroyed, so a disconnect followed by a fresh
/// connect can hand a new peer a departed peer's slot number — a <see cref="NetworkId"/> is
/// drawn from a monotonic server counter and is never reused for the lifetime of a match.
/// A client can therefore track each tank across the snapshot stream without two distinct
/// tanks ever aliasing onto one id.
/// <para>
/// The component is optional. A single-player or determinism-harness world spawns tanks
/// without one and <see cref="Garupan.Sim.Snapshot.SnapshotCapture"/> falls back to the
/// Arch slot id; only the authoritative match host stamps a real <see cref="NetworkId"/>.
/// </para>
/// <para>Ported from the <c>NetworkId</c> component of Tea's Family STO Phase 0.</para>
/// </summary>
public readonly record struct NetworkId(uint Value);
