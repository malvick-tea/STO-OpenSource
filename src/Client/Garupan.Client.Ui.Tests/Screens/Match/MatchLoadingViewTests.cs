using System.Linq;
using FluentAssertions;
using Garupan.Client.Ui.Screens.Match;
using Garupan.Client.Ui.Tests.Fixtures;
using Xunit;

namespace Garupan.Client.Ui.Tests.Screens.Match;

/// <summary>Pins the pre-play loading screen: the bar fills to the completed fraction and the
/// percentage / stage / connection text render. Headless over a <see cref="RecordingDrawSurface"/>.</summary>
public sealed class MatchLoadingViewTests
{
    [Fact]
    public void Fills_the_bar_to_the_completed_fraction()
    {
        var surface = new RecordingDrawSurface(1280, 720);

        MatchLoadingView.Draw(surface, progress: 0.5f, stageLabel: "Loading battlefield", subStatus: "Connecting…");

        var fill = surface.Commands.OfType<DrawFillRect>().Single(c => c.Color == NetworkMatchPalette.LoadingBar);
        fill.W.Should().Be(210, "half of the 420 px track at 50% progress");
    }

    [Fact]
    public void Renders_the_percentage_stage_and_substatus_text()
    {
        var surface = new RecordingDrawSurface(1280, 720);

        MatchLoadingView.Draw(surface, progress: 0.25f, stageLabel: "Loading battlefield", subStatus: "Connecting to Hungry Battles…");

        var texts = surface.Commands.OfType<DrawTextCommand>().Select(c => c.Text).ToList();
        texts.Should().Contain(t => t.Contains("25%"));
        texts.Should().Contain(t => t.Contains("Loading battlefield"));
        texts.Should().Contain("Connecting to Hungry Battles…");
    }

    [Fact]
    public void Clamps_progress_into_range_and_omits_an_empty_substatus()
    {
        var surface = new RecordingDrawSurface(1280, 720);

        MatchLoadingView.Draw(surface, progress: 1.5f, stageLabel: string.Empty, subStatus: string.Empty);

        var fill = surface.Commands.OfType<DrawFillRect>().Single(c => c.Color == NetworkMatchPalette.LoadingBar);
        fill.W.Should().Be(420, "progress clamps to 1");
        surface.Commands.OfType<DrawTextCommand>().Select(c => c.Text).Should().Contain("100%");
    }
}
