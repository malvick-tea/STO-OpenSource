using System.Linq;
using FluentAssertions;
using Garupan.Client.Ui.Screens.Match;
using Garupan.Client.Ui.Tests.Fixtures;
using Opus.Engine.Ui;
using Xunit;

namespace Garupan.Client.Ui.Tests.Screens.Match;

/// <summary>
/// Coverage for <see cref="NetworkMatchSkyBackdrop"/>: the banded sky+ground gradient fills
/// the whole surface, starts at the zenith colour, ends at the ground colour, and is split
/// by a horizon line at the configured fraction. Asserted over a <see cref="RecordingDrawSurface"/>.
/// </summary>
public sealed class NetworkMatchSkyBackdropTests
{
    private const int Width = 800;
    private const int Height = 600;

    private static readonly int HorizonY = (int)(Height * NetworkMatchSkyBackdrop.HorizonFraction);

    [Fact]
    public void The_backdrop_fills_the_full_surface_height_with_no_gap()
    {
        var surface = new RecordingDrawSurface(Width, Height);

        NetworkMatchSkyBackdrop.Draw(surface);

        var fills = surface.Commands.OfType<DrawFillRect>().ToList();
        fills.Should().NotBeEmpty();
        fills.Min(f => f.Y).Should().Be(0, "the sky starts at the top edge");
        fills.Max(f => f.Y + f.H).Should().Be(Height, "the ground reaches the bottom edge");
        fills.Should().OnlyContain(f => f.X == 0 && f.W == Width, "every band spans the full width");
    }

    [Fact]
    public void The_top_band_is_the_zenith_colour()
    {
        var surface = new RecordingDrawSurface(Width, Height);

        NetworkMatchSkyBackdrop.Draw(surface);

        var top = surface.Commands.OfType<DrawFillRect>().Single(f => f.Y == 0);
        top.Color.Should().Be(NetworkMatchSkyBackdrop.Zenith);
    }

    [Fact]
    public void The_bottom_band_is_the_far_ground_colour()
    {
        var surface = new RecordingDrawSurface(Width, Height);

        NetworkMatchSkyBackdrop.Draw(surface);

        var bottom = surface.Commands.OfType<DrawFillRect>().Single(f => f.Y + f.H == Height);
        bottom.Color.Should().Be(NetworkMatchSkyBackdrop.GroundFar);
    }

    [Fact]
    public void A_horizon_line_spans_the_width_at_the_horizon_fraction()
    {
        var surface = new RecordingDrawSurface(Width, Height);

        NetworkMatchSkyBackdrop.Draw(surface);

        var line = surface.Commands.OfType<DrawLineCommand>().Single();
        line.Y0.Should().Be(HorizonY);
        line.Y1.Should().Be(HorizonY);
        line.X0.Should().Be(0);
        line.X1.Should().Be(Width);
    }

    [Fact]
    public void A_zero_sized_surface_draws_nothing()
    {
        var surface = new RecordingDrawSurface(0, 0);

        NetworkMatchSkyBackdrop.Draw(surface);

        surface.Commands.Should().BeEmpty();
    }

    [Fact]
    public void The_sky_uses_a_fine_stable_procedural_gradient()
    {
        NetworkMatchSkyBackdrop.SkyBandCount.Should().BeGreaterThanOrEqualTo(48);
    }
}
