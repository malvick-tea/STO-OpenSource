using FluentAssertions;
using Garupan.Sim;
using Opus.Foundation;
using Xunit;

namespace Garupan.Sim.Tests.Loop;

public sealed class FixedStepLoopTests
{
    [Fact]
    public void Pump_with_zero_delta_fires_no_ticks()
    {
        var loop = new FixedStepLoop(60);
        var fired = loop.Pump(0);
        fired.Should().Be(0);
        loop.CurrentTick.Should().Be(Tick.Zero);
    }

    [Fact]
    public void Pump_one_tick_interval_fires_exactly_one_tick()
    {
        var loop = new FixedStepLoop(60);
        var fired = loop.Pump(1.0 / 60.0);
        fired.Should().Be(1);
        loop.CurrentTick.Value.Should().Be(1);
    }

    [Fact]
    public void Pump_three_intervals_fires_three_ticks()
    {
        var loop = new FixedStepLoop(60);
        var fired = loop.Pump(3.0 / 60.0);
        fired.Should().Be(3);
        loop.CurrentTick.Value.Should().Be(3);
    }

    [Fact]
    public void Pump_partial_interval_carries_remainder_into_next_pump()
    {
        var loop = new FixedStepLoop(60);
        loop.Pump(0.5 / 60.0).Should().Be(0);
        loop.Pump(0.6 / 60.0).Should().Be(1, "0.5 + 0.6 = 1.1 ticks accumulate to 1 fire");
    }

    [Fact]
    public void Pump_caps_runaway_to_avoid_spiral_of_death()
    {
        var loop = new FixedStepLoop(60);
        // 10 seconds of stall = 600 ticks — must clamp to maxTicksPerPump (8).
        var fired = loop.Pump(10.0);
        fired.Should().BeLessThanOrEqualTo(8);
    }

    [Fact]
    public void Tick_callback_invoked_with_correct_game_time()
    {
        var loop = new FixedStepLoop(60);
        long lastTick = -1;

        loop.OnTick = gt => lastTick = gt.Tick.Value;
        loop.Pump(2.0 / 60.0);

        lastTick.Should().Be(2);
    }

    [Fact]
    public void Alpha_reflects_partial_interval_progress()
    {
        var loop = new FixedStepLoop(60);
        loop.Pump(0.5 / 60.0); // half a tick into the accumulator
        loop.Alpha.Should().BeApproximately(0.5f, 0.01f);
    }
}
