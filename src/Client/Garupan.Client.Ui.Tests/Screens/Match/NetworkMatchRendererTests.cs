using System.Linq;
using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Garupan.Client.Ui.Screens.Match;
using Garupan.Client.Ui.Tests.Fixtures;
using Xunit;

namespace Garupan.Client.Ui.Tests.Screens.Match;

/// <summary>
/// Coverage for <see cref="NetworkMatchRenderer"/>'s notice draws — the match-over verdict
/// banner and the mode-mismatch band. Drives a <see cref="RecordingDrawSurface"/> so the
/// text, colour, and geometry are asserted without a GPU.
/// </summary>
public sealed class NetworkMatchRendererTests
{
    private const int SurfaceWidth = 1280;
    private const int SurfaceHeight = 720;

    [Theory]
    [InlineData(NetworkMatchVerdict.Victory, "VICTORY")]
    [InlineData(NetworkMatchVerdict.Defeat, "DEFEAT")]
    [InlineData(NetworkMatchVerdict.Draw, "DRAW")]
    public void DrawVerdict_renders_the_headline_for_each_verdict(NetworkMatchVerdict verdict, string expected)
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);

        NetworkMatchRenderer.DrawVerdict(surface, verdict);

        surface.Commands.OfType<DrawTextCommand>().Should().ContainSingle(t => t.Text == expected);
    }

    [Fact]
    public void DrawVerdict_paints_a_full_screen_scrim_first()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);

        NetworkMatchRenderer.DrawVerdict(surface, NetworkMatchVerdict.Victory);

        surface.Commands[0].Should().Be(
            new DrawFillRect(0, 0, SurfaceWidth, SurfaceHeight, NetworkMatchPalette.VerdictScrim));
    }

    [Fact]
    public void DrawVerdict_colours_the_headline_per_verdict()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);

        NetworkMatchRenderer.DrawVerdict(surface, NetworkMatchVerdict.Defeat);

        var headline = surface.Commands.OfType<DrawTextCommand>().Single(t => t.Text == "DEFEAT");
        headline.Color.Should().Be(NetworkMatchPalette.VerdictDefeat);
        headline.FontSize.Should().Be(NetworkMatchPalette.VerdictFontSize);
    }

    [Fact]
    public void DrawVerdict_centres_the_headline_horizontally()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);

        NetworkMatchRenderer.DrawVerdict(surface, NetworkMatchVerdict.Victory);

        var headline = surface.Commands.OfType<DrawTextCommand>().Single(t => t.Text == "VICTORY");
        var expectedX = (SurfaceWidth - surface.MeasureText("VICTORY", NetworkMatchPalette.VerdictFontSize)) / 2;
        headline.X.Should().Be(expectedX);
    }

    [Fact]
    public void DrawVerdict_renders_an_esc_to_leave_sub_line()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);

        NetworkMatchRenderer.DrawVerdict(surface, NetworkMatchVerdict.Draw);

        surface.Commands.OfType<DrawTextCommand>().Should().Contain(t => t.Text == "Esc to leave");
    }

    [Fact]
    public void DrawModeMismatch_names_both_the_joined_and_the_expected_mode()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);

        NetworkMatchRenderer.DrawModeMismatch(surface, expectedLabel: "HUNGRY BATTLES", actualLabel: "TACTICAL 5v5");

        var notice = surface.Commands.OfType<DrawTextCommand>().Single();
        notice.Text.Should().Contain("HUNGRY BATTLES").And.Contain("TACTICAL 5v5");
    }

    [Fact]
    public void DrawModeMismatch_warn_colours_the_notice()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);

        NetworkMatchRenderer.DrawModeMismatch(surface, "HUNGRY BATTLES", "TACTICAL 5v5");

        surface.Commands.OfType<DrawTextCommand>().Single().Color.Should().Be(NetworkMatchPalette.Warn);
    }

    [Fact]
    public void DrawModeMismatch_paints_a_full_width_band_below_the_top_bar()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);

        NetworkMatchRenderer.DrawModeMismatch(surface, "HUNGRY BATTLES", "TACTICAL 5v5");

        surface.Commands.OfType<DrawFillRect>()
            .Should().ContainSingle(r => r.Y == NetworkMatchPalette.TopBarHeight && r.W == SurfaceWidth);
    }
}
