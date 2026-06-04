using Garupan.Sim.Snapshot;
using Opus.Foundation;

namespace Garupan.Sim.Replay;

/// <summary>
/// One frame of a replay: a tick index plus a <see cref="WorldSnapshot"/> captured at
/// that tick. The reader hydrates the snapshot lazily from the on-wire bytes via
/// <see cref="SnapshotDecoder"/>; this record is the materialised result.
/// </summary>
public readonly record struct ReplayFrame(Tick Tick, WorldSnapshot Snapshot);
