using FluentAssertions;
using Garupan.Client.Ui.Match;
using Xunit;

namespace Garupan.Client.Ui.Tests.Match;

/// <summary>Pure mapping from gun reload timers to a HUD fraction. The bar reads
/// <c>(1 − remaining / max)</c> and is clamped so a numeric overshoot never breaks the
/// downstream pixel arithmetic.</summary>
public sealed class ReloadProgressTests
{
    [Fact]
    public void Max_of_zero_reads_as_ready()
    {
        ReloadProgress.Of(remainingSeconds: 0f, maxSeconds: 0f).Should().Be(1f);
        ReloadProgress.Of(remainingSeconds: 1.2f, maxSeconds: 0f).Should()
            .Be(1f, "a placeholder gun reads as ready rather than dividing by zero");
    }

    [Fact]
    public void Full_remaining_reads_as_zero_progress()
    {
        ReloadProgress.Of(remainingSeconds: 4f, maxSeconds: 4f).Should().Be(0f);
    }

    [Fact]
    public void Halfway_through_a_reload_reads_as_one_half()
    {
        ReloadProgress.Of(remainingSeconds: 2f, maxSeconds: 4f).Should().BeApproximately(0.5f, 1e-4f);
    }

    [Fact]
    public void Zero_remaining_reads_as_ready()
    {
        ReloadProgress.Of(remainingSeconds: 0f, maxSeconds: 4f).Should().Be(1f);
    }

    [Fact]
    public void Negative_remaining_clamps_to_ready()
    {
        ReloadProgress.Of(remainingSeconds: -0.5f, maxSeconds: 4f).Should().Be(1f);
    }

    [Fact]
    public void Remaining_above_max_clamps_to_zero()
    {
        ReloadProgress.Of(remainingSeconds: 8f, maxSeconds: 4f).Should().Be(0f);
    }
}
