using Opus.Foundation;

namespace Garupan.Sim.Replay;

/// <summary>
/// Header of a replay stream — emitted once at the start, read once on open. Tells the
/// reader the simulation rate the snapshots were sampled at, the absolute tick index
/// of the first frame, and how many frames follow.
///
/// Tick rate is recorded explicitly (rather than read from
/// <see cref="SimulationConstants.TicksPerSecond"/>) so a replay captured at one rate
/// can still be played back if the simulation rate ever changes in a future Garupan
/// version — the player consults this field instead of assuming.
/// </summary>
public readonly record struct ReplayHeader(
    uint Version,
    int TickRateHz,
    Tick StartTick,
    int FrameCount);
