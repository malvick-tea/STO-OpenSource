using System.Linq;
using FluentAssertions;
using Garupan.Client.Ui.Match;
using Garupan.Client.Ui.Tests.Fixtures;
using Xunit;

namespace Garupan.Client.Ui.Tests.Match;

/// <summary>
/// Covers <see cref="MatchHudReticle"/> — the crimson colour state, the aim line endpoints,
/// the four reticle arms with the centred gap, and the range label. Drawn through
/// <see cref="RecordingDrawSurface"/> so geometry is asserted, not rasterised.
/// </summary>
public sealed class MatchHudReticleTests
{
    [Fact]
    public void Ready_state_uses_crimson()
    {
        MatchHudReticle.ColorFor(isReady: true).Should().Be(MatchPalette.Crimson);
    }

    [Fact]
    public void Reloading_state_uses_the_dim_red()
    {
        MatchHudReticle.ColorFor(isReady: false).Should().Be(MatchPalette.ReticleReloading);
    }

    [Fact]
    public void Draw_emits_an_aim_line_from_the_player_to_the_aim_point()
    {
        var surface = new RecordingDrawSurface(800, 600);

        MatchHudReticle.Draw(surface, playerScreenX: 100, playerScreenY: 200, aimScreenX: 400, aimScreenY: 300, rangeMeters: 35.4f, isReady: true);

        var lines = surface.Commands.OfType<DrawLineCommand>().ToList();
        lines.Should().Contain(
            l => l.X0 == 100 && l.Y0 == 200 && l.X1 == 400 && l.Y1 == 300,
            "the first line should be the aim line from the player to the aim point");
    }

    [Fact]
    public void Draw_emits_four_reticle_arms_around_the_aim_point()
    {
        var surface = new RecordingDrawSurface(800, 600);

        MatchHudReticle.Draw(surface, playerScreenX: 0, playerScreenY: 0, aimScreenX: 400, aimScreenY: 300, rangeMeters: 0f, isReady: true);

        var armLines = surface.Commands.OfType<DrawLineCommand>()
            .Where(l => !(l.X0 == 0 && l.Y0 == 0)) // exclude the aim line itself
            .ToList();
        armLines.Should().HaveCount(4, "one line per crosshair arm: left, right, up, down");
        armLines.Should().Contain(l => l.Y0 == 300 && l.Y1 == 300, "two arms are horizontal at y=300");
        armLines.Should().Contain(l => l.X0 == 400 && l.X1 == 400, "two arms are vertical at x=400");
    }

    [Fact]
    public void Draw_writes_a_range_label_below_the_reticle()
    {
        var surface = new RecordingDrawSurface(800, 600);

        MatchHudReticle.Draw(surface, playerScreenX: 0, playerScreenY: 0, aimScreenX: 400, aimScreenY: 300, rangeMeters: 35.4f, isReady: true);

        var label = surface.Commands.OfType<DrawTextCommand>().Should().ContainSingle().Subject;
        label.Text.Should().Be("35 m");
        label.Y.Should().BeGreaterThan(300, "the range label sits below the reticle, not on top of it");
    }

    [Fact]
    public void Range_label_rounds_to_the_nearest_metre()
    {
        var surface = new RecordingDrawSurface(800, 600);

        MatchHudReticle.Draw(surface, playerScreenX: 0, playerScreenY: 0, aimScreenX: 400, aimScreenY: 300, rangeMeters: 12.6f, isReady: true);

        surface.Commands.OfType<DrawTextCommand>().Single().Text.Should().Be("13 m");
    }
}
