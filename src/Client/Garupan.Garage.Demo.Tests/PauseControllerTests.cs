using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Garupan.Garage.Demo.Tests;

/// <summary>Pure state-machine tests for <see cref="PauseController"/>. Covers initial
/// state, every transition direction, idempotence, and the <see cref="PauseController.Changed"/>
/// event contract.</summary>
public sealed class PauseControllerTests
{
    [Fact]
    public void Initial_state_is_not_paused()
    {
        new PauseController().IsPaused.Should().BeFalse();
    }

    [Fact]
    public void Toggle_flips_between_paused_and_running()
    {
        var ctl = new PauseController();
        ctl.Toggle();
        ctl.IsPaused.Should().BeTrue();
        ctl.Toggle();
        ctl.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void Pause_sets_state_true_and_Resume_sets_state_false()
    {
        var ctl = new PauseController();
        ctl.Pause();
        ctl.IsPaused.Should().BeTrue();
        ctl.Resume();
        ctl.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void Pause_is_idempotent_when_already_paused()
    {
        var ctl = new PauseController();
        var events = SubscribeEvents(ctl);

        ctl.Pause();
        ctl.Pause();
        ctl.Pause();

        ctl.IsPaused.Should().BeTrue();
        events.Should().Equal(true);
    }

    [Fact]
    public void Resume_is_idempotent_when_already_running()
    {
        var ctl = new PauseController();
        var events = SubscribeEvents(ctl);

        ctl.Resume();
        ctl.Resume();

        ctl.IsPaused.Should().BeFalse();
        events.Should().BeEmpty();
    }

    [Fact]
    public void Changed_fires_once_per_actual_transition()
    {
        var ctl = new PauseController();
        var events = SubscribeEvents(ctl);

        ctl.Toggle();   // → true
        ctl.Toggle();   // → false
        ctl.Pause();    // → true
        ctl.Resume();   // → false

        events.Should().Equal(true, false, true, false);
    }

    [Fact]
    public void Changed_carries_the_new_paused_state()
    {
        var ctl = new PauseController();
        bool? lastObserved = null;
        ctl.Changed += value => lastObserved = value;

        ctl.Pause();
        lastObserved.Should().Be(true);
        ctl.Resume();
        lastObserved.Should().Be(false);
    }

    private static List<bool> SubscribeEvents(PauseController ctl)
    {
        var events = new List<bool>();
        ctl.Changed += value => events.Add(value);
        return events;
    }
}
