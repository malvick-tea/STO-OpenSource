using Opus.Foundation;

namespace Garupan.Client.Ui.Navigation;

/// <summary>
/// Visual transition between screens. M2a supports two modes: instant cross-fade and a
/// timed alpha fade. Real curve-driven transitions land alongside the storyboard editor
/// (M5+).
/// </summary>
public readonly record struct ScreenTransition(ScreenTransitionKind Kind, float DurationSeconds)
{
    public static readonly ScreenTransition Instant = new(ScreenTransitionKind.Instant, 0f);

    public static ScreenTransition Fade(float seconds) =>
        new(ScreenTransitionKind.Fade, MathF.Max(0f, seconds));

    /// <summary>0 → start, 1 → done. Easing happens here later — linear for M2a.</summary>
    public float SampleProgress(float elapsed)
    {
        if (Kind == ScreenTransitionKind.Instant || DurationSeconds <= 0f)
        {
            return 1f;
        }

        var t = elapsed / DurationSeconds;
        return t < 0f ? 0f : (t > 1f ? 1f : t);
    }

    public bool IsDone(float elapsed) => Kind == ScreenTransitionKind.Instant || elapsed >= DurationSeconds;
}

public enum ScreenTransitionKind
{
    Instant,
    Fade,
}
