using FluentAssertions;
using Garupan.Client.Ui.Screens.Lobby;
using Garupan.Client.Ui.Tests.Fixtures;
using Xunit;

namespace Garupan.Client.Ui.Tests.Screens.Lobby;

/// <summary>Covers <see cref="SummaryLineWrapper.Wrap"/> — greedy word-wrap against the
/// surface's <c>MeasureText</c>. Uses the recording surface's deterministic monospace
/// approximation so line breaks are predictable.</summary>
public sealed class SummaryLineWrapperTests
{
    private const int FontSize = 12;

    [Fact]
    public void Empty_text_yields_no_lines()
    {
        var surface = new RecordingDrawSurface(800, 600);

        SummaryLineWrapper.Wrap(surface, string.Empty, maxWidth: 200, FontSize).Should().BeEmpty();
        SummaryLineWrapper.Wrap(surface, "   ", maxWidth: 200, FontSize).Should().BeEmpty();
    }

    [Fact]
    public void Single_short_line_fits_on_one_line()
    {
        var surface = new RecordingDrawSurface(800, 600);

        var lines = SummaryLineWrapper.Wrap(surface, "Hello world.", maxWidth: 500, FontSize);

        lines.Should().HaveCount(1);
        lines[0].Should().Be("Hello world.");
    }

    [Fact]
    public void Long_text_wraps_at_word_boundaries()
    {
        var surface = new RecordingDrawSurface(800, 600);

        // Picked so the first three words fit, the fourth would overflow at width=70.
        // RecordingDrawSurface uses MeasureText = len * 7 * fontSize / 12; at FontSize=12
        // that's 7 px per char. "one two three" = 13 chars = 91 px > 70. "one two" = 7
        // chars = 49 px <= 70.
        var lines = SummaryLineWrapper.Wrap(surface, "one two three four", maxWidth: 70, FontSize);

        lines.Should().HaveCountGreaterThan(1, "the input cannot fit on a single line at the chosen width");
        lines[0].Should().Be("one two");
    }

    [Fact]
    public void Zero_max_width_yields_no_lines()
    {
        var surface = new RecordingDrawSurface(800, 600);

        SummaryLineWrapper.Wrap(surface, "anything", maxWidth: 0, FontSize).Should().BeEmpty();
    }
}
