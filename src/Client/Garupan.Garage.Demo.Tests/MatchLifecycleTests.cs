using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Garupan.Garage.Demo.Tests;

/// <summary>Pure state-machine verification of <see cref="MatchLifecycle"/>. Captures
/// the OutcomeChanged event into a list and exercises the timer with synthetic deltas.</summary>
public sealed class MatchLifecycleTests
{
    [Fact]
    public void Tick_returns_false_while_outcome_is_InProgress_regardless_of_delta()
    {
        var lifecycle = new MatchLifecycle();
        var transitions = SubscribeTransitions(lifecycle);

        lifecycle.Tick(MatchOutcome.InProgress, MatchLifecycle.AutoRestartSeconds * 10).Should().BeFalse();
        transitions.Should().BeEmpty();
    }

    [Fact]
    public void Tick_fires_OutcomeChanged_once_on_InProgress_to_Victory()
    {
        var lifecycle = new MatchLifecycle();
        var transitions = SubscribeTransitions(lifecycle);

        lifecycle.Tick(MatchOutcome.Victory, deltaSeconds: 0.1).Should().BeFalse();
        lifecycle.Tick(MatchOutcome.Victory, deltaSeconds: 0.1).Should().BeFalse();

        transitions.Should().ContainSingle().Which.Should().Be(MatchOutcome.Victory);
    }

    [Fact]
    public void Tick_fires_OutcomeChanged_once_on_InProgress_to_Defeat()
    {
        var lifecycle = new MatchLifecycle();
        var transitions = SubscribeTransitions(lifecycle);

        lifecycle.Tick(MatchOutcome.Defeat, deltaSeconds: 0.1);

        transitions.Should().ContainSingle().Which.Should().Be(MatchOutcome.Defeat);
    }

    [Fact]
    public void Tick_signals_restart_once_AutoRestartSeconds_has_accumulated_on_Victory()
    {
        var lifecycle = new MatchLifecycle();
        lifecycle.Tick(MatchOutcome.Victory, MatchLifecycle.AutoRestartSeconds - 0.5).Should().BeFalse();
        lifecycle.Tick(MatchOutcome.Victory, 0.6).Should().BeTrue();
    }

    [Fact]
    public void Tick_signals_restart_once_AutoRestartSeconds_has_accumulated_on_Defeat()
    {
        var lifecycle = new MatchLifecycle();

        lifecycle.Tick(MatchOutcome.Defeat, MatchLifecycle.AutoRestartSeconds * 0.5).Should().BeFalse();
        lifecycle.Tick(MatchOutcome.Defeat, MatchLifecycle.AutoRestartSeconds * 0.6).Should().BeTrue();
    }

    [Fact]
    public void Tick_fires_OutcomeChanged_and_resets_timer_on_Victory_to_Defeat_transition()
    {
        var lifecycle = new MatchLifecycle();
        var transitions = SubscribeTransitions(lifecycle);

        lifecycle.Tick(MatchOutcome.Victory, MatchLifecycle.AutoRestartSeconds - 0.5);
        lifecycle.Tick(MatchOutcome.Defeat, deltaSeconds: 0.1).Should().BeFalse();

        transitions.Should().ContainInOrder(MatchOutcome.Victory, MatchOutcome.Defeat);

        // Timer reset on transition: full duration must elapse on the new outcome.
        lifecycle.Tick(MatchOutcome.Defeat, MatchLifecycle.AutoRestartSeconds - 0.3).Should().BeFalse();
        lifecycle.Tick(MatchOutcome.Defeat, 0.4).Should().BeTrue();
    }

    [Fact]
    public void Reset_clears_state_so_a_subsequent_Victory_refires_the_event()
    {
        var lifecycle = new MatchLifecycle();
        var transitions = SubscribeTransitions(lifecycle);

        lifecycle.Tick(MatchOutcome.Victory, 0.1);
        lifecycle.Reset();
        lifecycle.Tick(MatchOutcome.Victory, 0.1);

        transitions.Should().HaveCount(2).And.AllSatisfy(o => o.Should().Be(MatchOutcome.Victory));
    }

    [Fact]
    public void Reset_clears_accumulated_timer_so_the_threshold_must_be_crossed_again()
    {
        var lifecycle = new MatchLifecycle();
        lifecycle.Tick(MatchOutcome.Victory, MatchLifecycle.AutoRestartSeconds - 0.1);

        lifecycle.Reset();

        lifecycle.Tick(MatchOutcome.Victory, 0.5).Should().BeFalse();
    }

    [Fact]
    public void Tick_ignores_negative_and_non_finite_deltas_for_the_auto_restart_timer()
    {
        var lifecycle = new MatchLifecycle();
        lifecycle.Tick(MatchOutcome.Victory, deltaSeconds: 0.1);

        lifecycle.Tick(MatchOutcome.Victory, double.NaN).Should().BeFalse();
        lifecycle.Tick(MatchOutcome.Victory, double.PositiveInfinity).Should().BeFalse();
        lifecycle.Tick(MatchOutcome.Victory, deltaSeconds: -10.0).Should().BeFalse();

        // The earlier 0.1-second delta still counts; the bogus deltas don't push us over.
        lifecycle.Tick(MatchOutcome.Victory, MatchLifecycle.AutoRestartSeconds - 0.2).Should().BeFalse();
    }

    [Fact]
    public void Tick_returns_true_exactly_at_the_AutoRestartSeconds_threshold()
    {
        var lifecycle = new MatchLifecycle();
        lifecycle.Tick(MatchOutcome.Victory, MatchLifecycle.AutoRestartSeconds).Should().BeTrue();
    }

    [Fact]
    public void Tick_continues_to_return_true_after_the_threshold_is_exceeded()
    {
        // Useful guarantee: hosts can drop a frame and still see a restart signal next
        // tick rather than missing the edge.
        var lifecycle = new MatchLifecycle();
        lifecycle.Tick(MatchOutcome.Victory, MatchLifecycle.AutoRestartSeconds + 0.5).Should().BeTrue();
        lifecycle.Tick(MatchOutcome.Victory, 0.001).Should().BeTrue();
    }

    [Fact]
    public void OutcomeChanged_does_not_refire_on_repeated_same_outcome()
    {
        var lifecycle = new MatchLifecycle();
        var transitions = SubscribeTransitions(lifecycle);

        for (var i = 0; i < 10; i++)
        {
            lifecycle.Tick(MatchOutcome.Defeat, 0.01);
        }

        transitions.Should().ContainSingle();
    }

    [Fact]
    public void Same_outcome_followed_by_InProgress_then_same_outcome_again_fires_twice()
    {
        // E.g. Victory → restart → InProgress → another Victory: each Victory is a fresh
        // transition that should fire OutcomeChanged. This is exercised through the
        // Reset() path that hosts call after a restart.
        var lifecycle = new MatchLifecycle();
        var transitions = SubscribeTransitions(lifecycle);

        lifecycle.Tick(MatchOutcome.Victory, 0.01);
        lifecycle.Reset();
        lifecycle.Tick(MatchOutcome.InProgress, 0.01);
        lifecycle.Tick(MatchOutcome.Victory, 0.01);

        transitions.Should().HaveCount(2).And.AllSatisfy(o => o.Should().Be(MatchOutcome.Victory));
    }

    private static List<MatchOutcome> SubscribeTransitions(MatchLifecycle lifecycle)
    {
        var captured = new List<MatchOutcome>();
        lifecycle.OutcomeChanged += captured.Add;
        return captured;
    }
}
