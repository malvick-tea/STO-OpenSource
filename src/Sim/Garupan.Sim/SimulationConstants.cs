namespace Garupan.Sim;

/// <summary>
/// Authoritative simulation rate constants. Single source of truth — balance tables,
/// netcode, replay tooling, and the host frame loop all reference these so the per-tick
/// arithmetic stays in lock-step across modules.
///
/// Ported from <c>svo/engine/clock.h</c>. The C++ version keeps both an integer
/// nanosecond duration and a float-seconds value so systems that integrate in float
/// arithmetic read the same exactly-represented constant rather than each recomputing
/// from the integer duration; Garupan mirrors that — <see cref="TicksPerSecond"/> is
/// the integer ground truth, <see cref="FixedTimestepSeconds"/> is the float companion.
/// </summary>
public static class SimulationConstants
{
    /// <summary>Authoritative simulation rate, hertz. The concept document targets 60 Hz;
    /// every other rate (replay, snapshot cadence, network input frame rate) is derived
    /// from this so a future bump is one edit and not a hunt for hard-coded 60s.</summary>
    public const int TicksPerSecond = 60;

    /// <summary>Duration of one fixed simulation step, in float seconds. Computed at
    /// compile time so every system that converts the timestep to float reads the same
    /// exactly-represented value — divergence here would translate into per-system
    /// trajectory drift over thousands of ticks.</summary>
    public const float FixedTimestepSeconds = 1f / TicksPerSecond;

    /// <summary>Maximum wall time consumed by a single <c>FixedStepLoop.Pump</c> call.
    /// Excess time is dropped instead of producing a flood of catch-up ticks (the
    /// "spiral of death" failure mode after a paused debugger or an OS stall). 250 ms
    /// matches Glenn Fiedler's canonical "Fix Your Timestep" budget and the C++
    /// <c>Simulation::MAX_FRAME_TIME</c>.</summary>
    public const double MaxFrameSeconds = 0.250;
}
