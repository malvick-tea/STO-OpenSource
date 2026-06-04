using System;
using System.Collections.Generic;
using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Xunit;

namespace Garupan.Client.Ui.Tests.Match.Network;

/// <summary>Pins the frame-sliced loader the match screen drives one step per frame: ordered
/// execution, progress reporting, the active stage label, and safe over-advance.</summary>
public sealed class MatchScenePreloadTests
{
    [Fact]
    public void Advance_runs_steps_in_order_and_reports_progress_and_label()
    {
        var ran = new List<string>();
        var preload = new MatchScenePreload(new[]
        {
            new MatchPreloadStep("a", () => ran.Add("a")),
            new MatchPreloadStep("b", () => ran.Add("b")),
        });

        preload.Progress.Should().Be(0f);
        preload.StageLabel.Should().Be("a");
        preload.IsComplete.Should().BeFalse();

        preload.Advance().Should().BeTrue("one step still remains after the first");
        ran.Should().Equal("a");
        preload.Progress.Should().Be(0.5f);
        preload.StageLabel.Should().Be("b");

        preload.Advance().Should().BeFalse("the last step has run");
        ran.Should().Equal("a", "b");
        preload.Progress.Should().Be(1f);
        preload.IsComplete.Should().BeTrue();
        preload.StageLabel.Should().BeEmpty();
    }

    [Fact]
    public void Advance_on_a_complete_preload_is_a_safe_no_op()
    {
        var runs = 0;
        var preload = new MatchScenePreload(new[] { new MatchPreloadStep("only", () => runs++) });

        preload.Advance().Should().BeFalse();
        preload.Advance().Should().BeFalse("already complete — no further step");
        runs.Should().Be(1, "the single step runs exactly once");
    }

    [Fact]
    public void An_empty_preload_is_complete_from_the_outset()
    {
        var preload = new MatchScenePreload(Array.Empty<MatchPreloadStep>());

        preload.IsComplete.Should().BeTrue();
        preload.Progress.Should().Be(1f);
        preload.StageLabel.Should().BeEmpty();
        preload.Advance().Should().BeFalse();
    }
}
