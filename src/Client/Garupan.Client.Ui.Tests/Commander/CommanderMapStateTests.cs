using FluentAssertions;
using Garupan.Client.Ui.Commander;
using Opus.Engine.Ui;
using Xunit;

namespace Garupan.Client.Ui.Tests.Commander;

/// <summary>
/// Covers <see cref="CommanderMapState"/> — the stroke lifecycle, undo, clear, and the
/// <see cref="CommanderMapState.Changed"/> notification. Stroke order: insertion order;
/// the in-progress stroke is NOT part of <c>Strokes</c> until <see cref="CommanderMapState.End"/>.
/// </summary>
public sealed class CommanderMapStateTests
{
    private static readonly Color Ink = new(40, 30, 24, 255);

    [Fact]
    public void A_fresh_state_is_empty_and_not_drawing()
    {
        var state = new CommanderMapState();

        state.Strokes.Should().BeEmpty();
        state.IsDrawing.Should().BeFalse();
        state.ActivePoints.Should().BeEmpty();
    }

    [Fact]
    public void Begin_starts_an_active_stroke_with_one_vertex()
    {
        var state = new CommanderMapState();

        state.Begin(new CommanderMapPoint(10, 20), Ink, thickness: 3);

        state.IsDrawing.Should().BeTrue();
        state.ActivePoints.Should().ContainSingle().Which.Should().Be(new CommanderMapPoint(10, 20));
        state.Strokes.Should().BeEmpty("the stroke is not committed until End");
    }

    [Fact]
    public void Extend_appends_a_new_vertex_to_the_active_stroke()
    {
        var state = new CommanderMapState();
        state.Begin(new CommanderMapPoint(10, 20), Ink, thickness: 3);

        state.Extend(new CommanderMapPoint(15, 25));
        state.Extend(new CommanderMapPoint(20, 30));

        state.ActivePoints.Should().HaveCount(3);
        state.ActivePoints[2].Should().Be(new CommanderMapPoint(20, 30));
    }

    [Fact]
    public void Extending_with_the_same_point_is_a_silent_no_op()
    {
        var state = new CommanderMapState();
        state.Begin(new CommanderMapPoint(10, 20), Ink, thickness: 3);
        var fired = 0;
        state.Changed += () => fired++;

        state.Extend(new CommanderMapPoint(10, 20));

        state.ActivePoints.Should().HaveCount(1, "a duplicate same-pixel event must not bloat the buffer");
        fired.Should().Be(0, "a no-op extend must not raise Changed");
    }

    [Fact]
    public void End_commits_the_active_stroke_into_Strokes()
    {
        var state = new CommanderMapState();
        state.Begin(new CommanderMapPoint(10, 20), Ink, thickness: 3);
        state.Extend(new CommanderMapPoint(20, 20));

        state.End();

        state.IsDrawing.Should().BeFalse();
        state.Strokes.Should().HaveCount(1);
        state.Strokes[0].Points.Should().HaveCount(2);
        state.Strokes[0].InkColor.Should().Be(Ink);
    }

    [Fact]
    public void End_without_Begin_is_a_silent_no_op()
    {
        var state = new CommanderMapState();
        var fired = 0;
        state.Changed += () => fired++;

        state.End();

        state.Strokes.Should().BeEmpty();
        fired.Should().Be(0);
    }

    [Fact]
    public void Extend_without_Begin_is_ignored()
    {
        var state = new CommanderMapState();

        state.Extend(new CommanderMapPoint(10, 20));

        state.ActivePoints.Should().BeEmpty();
        state.IsDrawing.Should().BeFalse();
    }

    [Fact]
    public void A_second_Begin_without_End_commits_the_previous_stroke_defensively()
    {
        var state = new CommanderMapState();
        state.Begin(new CommanderMapPoint(0, 0), Ink, thickness: 3);
        state.Extend(new CommanderMapPoint(5, 5));

        state.Begin(new CommanderMapPoint(100, 100), Ink, thickness: 3);

        state.Strokes.Should().HaveCount(1, "a Begin without an intervening End must commit the previous stroke");
        state.IsDrawing.Should().BeTrue();
        state.ActivePoints[0].Should().Be(new CommanderMapPoint(100, 100));
    }

    [Fact]
    public void Undo_drops_the_most_recent_finished_stroke()
    {
        var state = new CommanderMapState();
        DrawCommittedStroke(state, x: 0, y: 0);
        DrawCommittedStroke(state, x: 100, y: 100);

        var undone = state.Undo();

        undone.Should().BeTrue();
        state.Strokes.Should().HaveCount(1);
        state.Strokes[0].Points[0].X.Should().Be(0, "the older stroke survives");
    }

    [Fact]
    public void Undo_on_an_empty_state_returns_false_and_stays_silent()
    {
        var state = new CommanderMapState();
        var fired = 0;
        state.Changed += () => fired++;

        var undone = state.Undo();

        undone.Should().BeFalse();
        fired.Should().Be(0);
    }

    [Fact]
    public void Clear_drops_every_stroke_and_the_in_progress_one()
    {
        var state = new CommanderMapState();
        DrawCommittedStroke(state, x: 0, y: 0);
        state.Begin(new CommanderMapPoint(50, 50), Ink, thickness: 3);

        state.Clear();

        state.Strokes.Should().BeEmpty();
        state.IsDrawing.Should().BeFalse();
        state.ActivePoints.Should().BeEmpty();
    }

    [Fact]
    public void Begin_clamps_thickness_to_a_positive_minimum()
    {
        var state = new CommanderMapState();

        state.Begin(new CommanderMapPoint(0, 0), Ink, thickness: 0);
        state.End();

        state.Strokes[0].Thickness.Should()
            .BeGreaterThanOrEqualTo(1, "an ink-zero stroke would be invisible — clamp instead of accept it");
    }

    [Fact]
    public void Changed_fires_once_per_real_mutation()
    {
        var state = new CommanderMapState();
        var fired = 0;
        state.Changed += () => fired++;

        state.Begin(new CommanderMapPoint(0, 0), Ink, thickness: 3);
        state.Extend(new CommanderMapPoint(5, 5));
        state.End();
        state.Undo();

        fired.Should().Be(4, "Begin + Extend + End + Undo are four visible mutations");
    }

    private static void DrawCommittedStroke(CommanderMapState state, int x, int y)
    {
        state.Begin(new CommanderMapPoint(x, y), Ink, thickness: 3);
        state.Extend(new CommanderMapPoint(x + 10, y + 10));
        state.End();
    }
}
