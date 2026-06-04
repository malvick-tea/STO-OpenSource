using System.Linq;
using FluentAssertions;
using Garupan.Client.Ui.Commander;
using Garupan.Client.Ui.Tests.Fixtures;
using Opus.Engine.Ui;
using Xunit;

namespace Garupan.Client.Ui.Tests.Commander;

/// <summary>
/// Covers <see cref="CommanderMapRenderer"/> — paper / border / grid / strokes / active
/// stroke. Drives the renderer against a <see cref="RecordingDrawSurface"/> so geometry
/// is asserted, not rasterised.
/// </summary>
public sealed class CommanderMapRendererTests
{
    private static readonly CommanderMapBounds Paper = new(X: 100, Y: 100, Width: 400, Height: 300);
    private static readonly Color Ink = new(40, 30, 24, 255);

    [Fact]
    public void Render_paints_the_paper_rectangle_in_the_paper_colour()
    {
        var surface = new RecordingDrawSurface(800, 600);
        var state = new CommanderMapState();

        new CommanderMapRenderer().Render(surface, Paper, state);

        surface.Commands.OfType<DrawFillRect>().Should().Contain(r =>
            r.X == Paper.X && r.Y == Paper.Y && r.W == Paper.Width && r.H == Paper.Height);
    }

    [Fact]
    public void Render_strokes_the_paper_border_on_top_of_the_grid()
    {
        var surface = new RecordingDrawSurface(800, 600);

        new CommanderMapRenderer().Render(surface, Paper, new CommanderMapState());

        surface.Commands.OfType<DrawStrokeRect>().Should().ContainSingle().Which.Thickness.Should().Be(3);
    }

    [Fact]
    public void Render_emits_one_line_segment_per_pair_of_committed_stroke_vertices()
    {
        var surface = new RecordingDrawSurface(800, 600);
        var state = new CommanderMapState();
        state.Begin(new CommanderMapPoint(120, 120), Ink, thickness: 3);
        state.Extend(new CommanderMapPoint(140, 130));
        state.Extend(new CommanderMapPoint(160, 140));
        state.End();

        new CommanderMapRenderer().Render(surface, Paper, state);

        var inkLines = surface.Commands.OfType<DrawLineCommand>().Where(l => l.Color == Ink).ToList();
        inkLines.Should().HaveCount(2);
        inkLines[0].X0.Should().Be(120);
        inkLines[1].X1.Should().Be(160);
    }

    [Fact]
    public void A_one_vertex_stroke_renders_as_a_dot_not_a_line()
    {
        var surface = new RecordingDrawSurface(800, 600);
        var state = new CommanderMapState();
        state.Begin(new CommanderMapPoint(200, 200), Ink, thickness: 4);
        state.End();

        new CommanderMapRenderer().Render(surface, Paper, state);

        surface.Commands.OfType<DrawFillCircle>().Should().Contain(c =>
            c.Cx == 200 && c.Cy == 200 && c.Color == Ink);
        surface.Commands.OfType<DrawLineCommand>().Should()
            .NotContain(l => l.Color == Ink, "a dot does not draw any ink line segments");
    }

    [Fact]
    public void Active_stroke_is_drawn_on_top_of_committed_strokes()
    {
        var surface = new RecordingDrawSurface(800, 600);
        var state = new CommanderMapState();
        state.Begin(new CommanderMapPoint(120, 120), Ink, thickness: 3);
        state.Extend(new CommanderMapPoint(130, 130));
        state.End();
        state.Begin(new CommanderMapPoint(200, 200), Ink, thickness: 3); // not ended yet

        new CommanderMapRenderer().Render(surface, Paper, state);

        var inkOps = surface.Commands
            .Where(c => (c is DrawLineCommand ll && ll.Color == Ink) || (c is DrawFillCircle fc && fc.Color == Ink))
            .ToList();
        inkOps.Should().HaveCountGreaterThanOrEqualTo(2, "both the committed line and the active dot must render");
    }

    [Fact]
    public void Clear_at_render_time_produces_paper_but_no_ink()
    {
        var surface = new RecordingDrawSurface(800, 600);

        new CommanderMapRenderer().Render(surface, Paper, new CommanderMapState());

        surface.Commands.OfType<DrawLineCommand>().Should().NotContain(l => l.Color == Ink);
        surface.Commands.OfType<DrawFillCircle>().Should().NotContain(c => c.Color == Ink);
    }
}
