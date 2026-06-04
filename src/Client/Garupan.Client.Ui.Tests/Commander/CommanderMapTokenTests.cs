using System.Linq;
using FluentAssertions;
using Garupan.Client.Ui.Commander;
using Garupan.Client.Ui.Tests.Fixtures;
using Opus.Engine.Ui;
using Xunit;

namespace Garupan.Client.Ui.Tests.Commander;

/// <summary>
/// Covers the token side of <see cref="CommanderMapState"/> + the sequential auto-label
/// helper + the renderer's token glyph. Tokens share the Marks list with strokes — these
/// tests verify the shared-Undo behaviour and the per-type filtered views.
/// </summary>
public sealed class CommanderMapTokenTests
{
    private static readonly Color Ink = new(40, 30, 24, 255);
    private static readonly CommanderMapBounds Paper = new(X: 0, Y: 0, Width: 800, Height: 600);

    [Fact]
    public void PlaceToken_adds_a_token_mark()
    {
        var state = new CommanderMapState();

        state.PlaceToken(new CommanderMapPoint(120, 80), Ink, label: "1");

        state.Tokens.Should().ContainSingle();
        state.Tokens[0].Label.Should().Be("1");
        state.Tokens[0].Position.Should().Be(new CommanderMapPoint(120, 80));
        state.Marks.Should().HaveCount(1);
    }

    [Fact]
    public void Auto_label_numbers_tokens_sequentially()
    {
        var state = new CommanderMapState();

        CommanderMapTokenLabels.Next(state).Should().Be("1");
        state.PlaceToken(new CommanderMapPoint(10, 10), Ink, CommanderMapTokenLabels.Next(state));
        CommanderMapTokenLabels.Next(state).Should().Be("2");
        state.PlaceToken(new CommanderMapPoint(20, 20), Ink, CommanderMapTokenLabels.Next(state));
        CommanderMapTokenLabels.Next(state).Should().Be("3");
    }

    [Fact]
    public void Auto_label_reuses_numbers_after_undo()
    {
        var state = new CommanderMapState();
        state.PlaceToken(new CommanderMapPoint(10, 10), Ink, CommanderMapTokenLabels.Next(state));
        state.PlaceToken(new CommanderMapPoint(20, 20), Ink, CommanderMapTokenLabels.Next(state));

        state.Undo();

        CommanderMapTokenLabels.Next(state).Should().Be(
            "2",
            "sequential labelling is gap-aware via token count, so undo frees the number again");
    }

    [Fact]
    public void Undo_pops_the_most_recent_mark_regardless_of_kind()
    {
        var state = new CommanderMapState();
        state.Begin(new CommanderMapPoint(0, 0), Ink, thickness: 3);
        state.Extend(new CommanderMapPoint(10, 10));
        state.End();
        state.PlaceToken(new CommanderMapPoint(100, 100), Ink, "1");

        state.Undo();

        state.Tokens.Should().BeEmpty();
        state.Strokes.Should().HaveCount(1, "the older stroke survives — undo popped the newer token");

        state.Undo();

        state.Marks.Should().BeEmpty();
    }

    [Fact]
    public void Marks_preserves_placement_order_across_types()
    {
        var state = new CommanderMapState();
        state.Begin(new CommanderMapPoint(0, 0), Ink, thickness: 3);
        state.End();
        state.PlaceToken(new CommanderMapPoint(100, 100), Ink, "1");
        state.Begin(new CommanderMapPoint(50, 50), Ink, thickness: 3);
        state.End();

        state.Marks.Should().HaveCount(3);
        state.Marks[0].Should().BeOfType<CommanderMapStroke>();
        state.Marks[1].Should().BeOfType<CommanderMapToken>();
        state.Marks[2].Should().BeOfType<CommanderMapStroke>();
    }

    [Fact]
    public void Clear_removes_strokes_and_tokens()
    {
        var state = new CommanderMapState();
        state.Begin(new CommanderMapPoint(0, 0), Ink, thickness: 3);
        state.End();
        state.PlaceToken(new CommanderMapPoint(100, 100), Ink, "1");

        state.Clear();

        state.Strokes.Should().BeEmpty();
        state.Tokens.Should().BeEmpty();
        state.Marks.Should().BeEmpty();
    }

    [Fact]
    public void Renderer_emits_an_outline_and_label_for_each_token()
    {
        var surface = new RecordingDrawSurface(800, 600);
        var state = new CommanderMapState();
        state.PlaceToken(new CommanderMapPoint(120, 80), Ink, "1");
        state.PlaceToken(new CommanderMapPoint(200, 150), Ink, "2");

        new CommanderMapRenderer().Render(surface, Paper, state);

        var inkOutlines = surface.Commands.OfType<DrawStrokeCircle>().Where(c => c.Color == Ink).ToList();
        inkOutlines.Should().HaveCount(2, "one outline per token");
        var inkLabels = surface.Commands.OfType<DrawTextCommand>().Where(t => t.Color == Ink).ToList();
        inkLabels.Select(t => t.Text).Should().BeEquivalentTo(new[] { "1", "2" });
    }
}
