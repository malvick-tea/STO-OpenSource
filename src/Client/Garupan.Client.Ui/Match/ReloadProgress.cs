using System;

namespace Garupan.Client.Ui.Match;

/// <summary>
/// Pure mapping from the live <see cref="Sim.Components.Gun"/> reload timers to a HUD
/// fraction in <c>[0, 1]</c> — 0 = just fired, 1 = ready to fire. Pulled out so the math
/// is one unit-testable line, independent of the ECS read in
/// <see cref="MatchHudReadout.Capture"/>.
/// </summary>
public static class ReloadProgress
{
    /// <summary>
    /// Returns the reload progress fraction. <paramref name="maxSeconds"/> ≤ 0 (a gun that
    /// has never fired, or a phase-0 placeholder) reads as fully ready; otherwise the
    /// fraction is <c>1 − remaining / max</c> clamped into <c>[0, 1]</c> so a numeric
    /// overshoot can't break the bar layout downstream.
    /// </summary>
    public static float Of(float remainingSeconds, float maxSeconds)
    {
        if (maxSeconds <= 0f)
        {
            return 1f;
        }

        var fraction = 1f - (remainingSeconds / maxSeconds);
        return Math.Clamp(fraction, 0f, 1f);
    }
}
