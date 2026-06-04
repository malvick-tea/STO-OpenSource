using System;

namespace Garupan.Garage.Demo;

/// <summary>
/// Tracks high-level demo-match transitions across frames: detects when
/// <see cref="MatchOutcome"/> changes, accumulates wall-clock seconds since the latest
/// non-InProgress outcome, and signals an automatic restart once
/// <see cref="AutoRestartSeconds"/> has elapsed. Pure state machine — no I/O, no
/// timing dependency beyond the <paramref name="deltaSeconds"/> the caller supplies.
/// </summary>
/// <remarks>
/// Wiring shape: caller subscribes to <see cref="OutcomeChanged"/> for outcome-transition
/// side effects (console log, future HUD overlay, audio cue), polls <see cref="Tick"/>
/// every frame to advance the timer + sample <c>true</c> when the auto-restart threshold
/// fires. <see cref="Reset"/> is called the same frame the host actually performs the
/// restart so the next outcome-transition fires cleanly.
/// </remarks>
internal sealed class MatchLifecycle
{
    /// <summary>Seconds the demo waits at a non-InProgress outcome before signalling an
    /// automatic match restart. Long enough to read the outcome message; short enough
    /// that the player isn't staring at a tilted tank indefinitely.</summary>
    public const double AutoRestartSeconds = 3.0;

    private MatchOutcome _lastOutcome = MatchOutcome.InProgress;
    private double _secondsSinceOutcome;

    /// <summary>Raised once per actual outcome transition (e.g. InProgress → Victory).
    /// Repeated calls to <see cref="Tick"/> with the same outcome do not refire.</summary>
    public event Action<MatchOutcome>? OutcomeChanged;

    /// <summary>Resets the lifecycle to a fresh-match baseline. Call the same frame the
    /// host swaps the sim for a new <c>CreateMatch()</c> instance, so the next
    /// InProgress→Victory/Defeat transition fires <see cref="OutcomeChanged"/>.</summary>
    public void Reset()
    {
        _lastOutcome = MatchOutcome.InProgress;
        _secondsSinceOutcome = 0.0;
    }

    /// <summary>Advances the lifecycle by <paramref name="deltaSeconds"/> against the
    /// current observed <paramref name="outcome"/>. Returns <c>true</c> exactly when the
    /// auto-restart threshold has been crossed for the current non-InProgress outcome.
    /// Non-positive and non-finite deltas are clamped to zero.</summary>
    public bool Tick(MatchOutcome outcome, double deltaSeconds)
    {
        if (outcome != _lastOutcome)
        {
            _lastOutcome = outcome;
            _secondsSinceOutcome = 0.0;
            OutcomeChanged?.Invoke(outcome);
        }

        if (outcome == MatchOutcome.InProgress)
        {
            return false;
        }

        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0.0)
        {
            return false;
        }

        _secondsSinceOutcome += deltaSeconds;
        return _secondsSinceOutcome >= AutoRestartSeconds;
    }
}
