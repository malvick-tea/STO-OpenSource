using System;

namespace Garupan.Garage.Demo;

/// <summary>Pure state machine for the demo's pause toggle. Owns a single boolean —
/// <see cref="IsPaused"/> — and fires <see cref="Changed"/> on every actual transition.
/// The main loop polls <see cref="IsPaused"/> to decide whether to advance the sim +
/// casing ejector + orbit camera; rendering still runs every frame so the player sees
/// the frozen last frame, not a black screen.</summary>
/// <remarks>
/// Mirrors the <see cref="MatchLifecycle"/> shape (state + Changed event + Reset on
/// restart). No timing, no I/O — the host calls <see cref="Toggle"/> /
/// <see cref="Pause"/> / <see cref="Resume"/> in response to player input or auto-resume
/// events. Idempotent: calling <see cref="Pause"/> while already paused is a no-op and
/// does not refire <see cref="Changed"/>.
/// </remarks>
public sealed class PauseController
{
    /// <summary>Raised once per actual transition (false→true or true→false). Repeated
    /// idempotent calls do NOT refire.</summary>
    public event Action<bool>? Changed;

    public bool IsPaused { get; private set; }

    public void Toggle() => SetPaused(!IsPaused);

    public void Pause() => SetPaused(true);

    public void Resume() => SetPaused(false);

    private void SetPaused(bool value)
    {
        if (IsPaused == value)
        {
            return;
        }

        IsPaused = value;
        Changed?.Invoke(value);
    }
}
