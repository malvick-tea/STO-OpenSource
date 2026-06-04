using FluentAssertions;
using Garupan.Client.Ui.Commander;
using Garupan.Client.Ui.Tests.Fixtures;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Xunit;

namespace Garupan.Client.Ui.Tests.Commander;

/// <summary>
/// Covers <see cref="CommanderMapInput"/> — the rising-edge stroke start, the level-held
/// extension, the synthesised falling-edge release, the "cursor left the paper"
/// termination, the per-tool dispatch (pencil vs marker vs token), and the
/// commit-on-tool-switch behaviour. Drives a real <see cref="CommanderMapState"/> so the
/// tests assert on the outcome, not on the input bridge's private state.
/// </summary>
public sealed class CommanderMapInputTests
{
    private static readonly CommanderMapBounds Paper = new(X: 100, Y: 100, Width: 400, Height: 300);
    private static readonly Color Ink = new(40, 30, 24, 255);

    [Fact]
    public void A_pencil_press_inside_the_paper_starts_a_stroke()
    {
        var state = new CommanderMapState();
        var input = new CommanderMapInput();
        var frame = new FakeInputSource().At(200, 200).PressMouse(MouseButton.Left);

        input.Update(frame, Paper, CommanderTool.Pencil, Ink, state);

        state.IsDrawing.Should().BeTrue();
        state.ActivePoints.Should().ContainSingle().Which.Should().Be(new CommanderMapPoint(200, 200));
    }

    [Fact]
    public void A_press_outside_the_paper_does_nothing()
    {
        var state = new CommanderMapState();
        var input = new CommanderMapInput();
        var frame = new FakeInputSource().At(10, 10).PressMouse(MouseButton.Left);

        input.Update(frame, Paper, CommanderTool.Pencil, Ink, state);

        state.IsDrawing.Should().BeFalse();
        state.Strokes.Should().BeEmpty();
    }

    [Fact]
    public void A_held_drag_extends_the_active_stroke()
    {
        var (state, input) = StartedStroke(startX: 200, startY: 200);

        input.Update(new FakeInputSource().At(220, 210).HoldMouse(MouseButton.Left), Paper, CommanderTool.Pencil, Ink, state);
        input.Update(new FakeInputSource().At(240, 220).HoldMouse(MouseButton.Left), Paper, CommanderTool.Pencil, Ink, state);

        state.ActivePoints.Should().HaveCount(3);
        state.ActivePoints[2].Should().Be(new CommanderMapPoint(240, 220));
    }

    [Fact]
    public void Releasing_the_button_ends_the_stroke()
    {
        var (state, input) = StartedStroke(startX: 200, startY: 200);
        input.Update(new FakeInputSource().At(220, 210).HoldMouse(MouseButton.Left), Paper, CommanderTool.Pencil, Ink, state);

        // Frame with the button released — no Hold/Press.
        input.Update(new FakeInputSource().At(220, 210), Paper, CommanderTool.Pencil, Ink, state);

        state.IsDrawing.Should().BeFalse();
        state.Strokes.Should().HaveCount(1);
        state.Strokes[0].Points.Should().HaveCount(2);
    }

    [Fact]
    public void Sweeping_off_the_paper_while_holding_ends_the_stroke_cleanly()
    {
        var (state, input) = StartedStroke(startX: 200, startY: 200);

        input.Update(new FakeInputSource().At(10, 10).HoldMouse(MouseButton.Left), Paper, CommanderTool.Pencil, Ink, state);

        state.IsDrawing.Should().BeFalse();
        state.Strokes.Should().HaveCount(1);
    }

    [Fact]
    public void Returning_to_the_paper_after_a_sweep_off_starts_a_new_stroke_on_the_next_press()
    {
        var (state, input) = StartedStroke(startX: 200, startY: 200);
        input.Update(new FakeInputSource().At(10, 10).HoldMouse(MouseButton.Left), Paper, CommanderTool.Pencil, Ink, state);
        input.Update(new FakeInputSource().At(10, 10), Paper, CommanderTool.Pencil, Ink, state);
        input.Update(new FakeInputSource().At(150, 150).PressMouse(MouseButton.Left), Paper, CommanderTool.Pencil, Ink, state);

        state.Strokes.Should().HaveCount(1, "the first sweep finished as one stroke");
        state.IsDrawing.Should().BeTrue();
        state.ActivePoints[0].Should().Be(new CommanderMapPoint(150, 150));
    }

    [Fact]
    public void Pencil_strokes_are_thinner_than_marker_strokes()
    {
        var stateA = new CommanderMapState();
        var stateB = new CommanderMapState();
        var inputA = new CommanderMapInput();
        var inputB = new CommanderMapInput();

        inputA.Update(new FakeInputSource().At(200, 200).PressMouse(MouseButton.Left), Paper, CommanderTool.Pencil, Ink, stateA);
        inputA.Update(new FakeInputSource().At(200, 200), Paper, CommanderTool.Pencil, Ink, stateA);
        inputB.Update(new FakeInputSource().At(200, 200).PressMouse(MouseButton.Left), Paper, CommanderTool.Marker, Ink, stateB);
        inputB.Update(new FakeInputSource().At(200, 200), Paper, CommanderTool.Marker, Ink, stateB);

        stateA.Strokes[0].Thickness.Should().BeLessThan(stateB.Strokes[0].Thickness);
    }

    [Fact]
    public void A_token_press_places_one_token_and_does_not_start_a_stroke()
    {
        var state = new CommanderMapState();
        var input = new CommanderMapInput();
        var frame = new FakeInputSource().At(200, 200).PressMouse(MouseButton.Left);

        input.Update(frame, Paper, CommanderTool.Token, Ink, state);

        state.Tokens.Should().ContainSingle().Which.Position.Should().Be(new CommanderMapPoint(200, 200));
        state.IsDrawing.Should().BeFalse("token tool does not start a stroke");
    }

    [Fact]
    public void Holding_the_button_in_token_mode_does_not_place_more_tokens()
    {
        var state = new CommanderMapState();
        var input = new CommanderMapInput();

        input.Update(new FakeInputSource().At(200, 200).PressMouse(MouseButton.Left), Paper, CommanderTool.Token, Ink, state);
        input.Update(new FakeInputSource().At(210, 210).HoldMouse(MouseButton.Left), Paper, CommanderTool.Token, Ink, state);
        input.Update(new FakeInputSource().At(220, 220).HoldMouse(MouseButton.Left), Paper, CommanderTool.Token, Ink, state);

        state.Tokens.Should().HaveCount(1, "token placement is rising-edge only");
    }

    [Fact]
    public void A_token_press_outside_the_paper_places_nothing()
    {
        var state = new CommanderMapState();
        var input = new CommanderMapInput();

        input.Update(new FakeInputSource().At(10, 10).PressMouse(MouseButton.Left), Paper, CommanderTool.Token, Ink, state);

        state.Tokens.Should().BeEmpty();
    }

    [Fact]
    public void Switching_tool_mid_stroke_commits_the_active_stroke()
    {
        var (state, input) = StartedStroke(startX: 200, startY: 200);

        // Tool switches to Token while the button is still held — pencil stroke should commit.
        input.Update(new FakeInputSource().At(220, 210).HoldMouse(MouseButton.Left), Paper, CommanderTool.Token, Ink, state);

        state.IsDrawing.Should().BeFalse();
        state.Strokes.Should().HaveCount(1, "the pencil stroke committed on tool switch");
    }

    [Fact]
    public void Reset_treats_a_held_button_as_a_new_rising_edge_on_the_next_frame()
    {
        // Use case: screen re-entered while the user is still holding the button. The
        // previous frame's _wasDown=true would suppress the rising edge — Reset clears it
        // so the held button reads as a fresh press, starting a new stroke.
        var (state, input) = StartedStroke(startX: 200, startY: 200);
        state.End();

        input.Reset();
        input.Update(new FakeInputSource().At(250, 250).HoldMouse(MouseButton.Left), Paper, CommanderTool.Pencil, Ink, state);

        state.IsDrawing.Should().BeTrue();
        state.ActivePoints[0].Should().Be(new CommanderMapPoint(250, 250));
        state.Strokes.Should().HaveCount(1, "the previously committed stroke survives");
    }

    private static (CommanderMapState State, CommanderMapInput Input) StartedStroke(int startX, int startY)
    {
        var state = new CommanderMapState();
        var input = new CommanderMapInput();
        input.Update(new FakeInputSource().At(startX, startY).PressMouse(MouseButton.Left), Paper, CommanderTool.Pencil, Ink, state);
        return (state, input);
    }
}
