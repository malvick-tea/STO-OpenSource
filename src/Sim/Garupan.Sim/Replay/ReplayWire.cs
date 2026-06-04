namespace Garupan.Sim.Replay;

/// <summary>
/// Wire-format constants for the replay stream. A replay is a sequence of
/// <c>WorldSnapshot</c> frames captured at the simulation tick rate; the snapshot bytes
/// inside each frame use <see cref="Snapshot.SnapshotWire"/>'s layout verbatim so a
/// future "scrub to tick N" tool can decode any single frame without reading the rest.
///
/// File layout:
/// <code>
/// header (24 bytes):
///   [ 0.. 4)  magic "SVOR"
///   [ 4.. 8)  replay version (uint32 LE)
///   [ 8..12)  tick rate Hz (uint32 LE)
///   [12..20)  start tick (uint64 LE) — the first frame's tick index
///   [20..24)  frame count (uint32 LE)
///
/// frame (variable, frame header 8 bytes + N snapshot bytes):
///   [ 0.. 4)  tick offset from start tick (uint32 LE)
///   [ 4.. 8)  snapshot length L (uint32 LE)
///   [ 8.. 8+L) snapshot bytes (see SnapshotWire)
/// </code>
///
/// This is a Garupan-side artefact (no C++ counterpart). The encoded byte sequence is
/// suitable for golden-hash determinism tests, a "watch replay" UI, and post-mortem
/// debugging.
/// </summary>
public static class ReplayWire
{
    /// <summary>Replay format version. Bumped any time the framing changes in a way
    /// previous readers can't parse. 1 = initial layout.</summary>
    public const uint Version = 1;

    public const int HeaderBytes = 24;
    public const int FrameHeaderBytes = 8;

    /// <summary>Magic prefix on every replay stream: ASCII "SVOR" (S V O Replay).</summary>
    public static readonly byte[] Magic = { (byte)'S', (byte)'V', (byte)'O', (byte)'R' };
}
