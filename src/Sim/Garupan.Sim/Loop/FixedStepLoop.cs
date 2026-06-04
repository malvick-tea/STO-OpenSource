using Opus.Foundation;

namespace Garupan.Sim;

/// <summary>
/// Fixed-step accumulator. Host feeds it real-frame deltas; the loop fires
/// <see cref="OnTick"/> as many times per frame as needed to stay in sync with the
/// configured tick rate. Time is the only source of truth — no wall-clock anywhere.
///
/// Render-side interpolation: the host queries <see cref="Alpha"/> after pumping to
/// blend between previous and current world snapshots. Engine.Renderer uses this in
/// the SimToRenderProjector layer.
///
/// Determinism contract: identical (deltaSeconds sequence) MUST produce identical
/// (Tick count, system invocation order). Enforced by golden replay tests in M3+.
/// </summary>
public sealed class FixedStepLoop
{
    private readonly double _tickInterval;
    private double _accumulator;
    private GameTime _now;

    public FixedStepLoop(int tickRateHz = GameTime.DefaultTickRateHz)
    {
        Ensure.InRange(tickRateHz, 1, 1000);
        _tickInterval = 1.0 / tickRateHz;
        _now = GameTime.AtRate(tickRateHz);
    }

    /// <summary>Number of fixed-step ticks fired since loop construction.</summary>
    public Tick CurrentTick => _now.Tick;

    /// <summary>Interpolation factor in [0..1] for the *next* frame's render blend.</summary>
    public float Alpha => (float)(_accumulator / _tickInterval);

    /// <summary>Invoked once per fixed-step tick. Set by host, called by <see cref="Pump"/>.</summary>
    public System.Action<GameTime>? OnTick { get; set; }

    /// <summary>Advance the accumulator by a real-frame delta and dispatch as many ticks as fit.</summary>
    /// <returns>Number of ticks that fired this pump.</returns>
    public int Pump(double frameDeltaSeconds)
    {
        if (frameDeltaSeconds <= 0)
        {
            return 0;
        }

        _accumulator += frameDeltaSeconds;
        var fired = 0;

        // Cap to avoid spiral-of-death after a long stall.
        const int maxTicksPerPump = 8;

        while (_accumulator >= _tickInterval && fired < maxTicksPerPump)
        {
            _now = _now.Advance();
            OnTick?.Invoke(_now);
            _accumulator -= _tickInterval;
            fired++;
        }

        if (fired == maxTicksPerPump)
        {
            // Drop excess accumulated time so we don't try to catch up forever.
            _accumulator = 0;
        }

        return fired;
    }
}
