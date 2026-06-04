using FluentAssertions;
using Garupan.Client.Ui.Screens.Match;
using Xunit;

namespace Garupan.Client.Ui.Tests.Screens.Match;

/// <summary>
/// Covers <see cref="MatchPauseOverlay.ActionAt"/> — the pause-menu button hit-test, on a
/// 1280×720 surface.
/// </summary>
public sealed class MatchPauseOverlayTests
{
    private const int Width = 1280;
    private const int Height = 720;

    [Fact]
    public void Centre_of_the_resume_button_resolves_to_Resume()
    {
        // Resume button: x [490, 790), y [334, 380).
        var action = MatchPauseOverlay.ActionAt(640, 357, Width, Height);

        action.Should().Be(PauseAction.Resume);
    }

    [Fact]
    public void Centre_of_the_abandon_button_resolves_to_Abandon()
    {
        // Abandon button: x [490, 790), y [394, 440).
        var action = MatchPauseOverlay.ActionAt(640, 417, Width, Height);

        action.Should().Be(PauseAction.Abandon);
    }

    [Fact]
    public void A_point_in_the_gap_between_the_buttons_resolves_to_None()
    {
        var action = MatchPauseOverlay.ActionAt(640, 386, Width, Height);

        action.Should().Be(PauseAction.None);
    }

    [Fact]
    public void A_point_outside_the_panel_resolves_to_None()
    {
        MatchPauseOverlay.ActionAt(10, 10, Width, Height).Should().Be(PauseAction.None);
        MatchPauseOverlay.ActionAt(640, 270, Width, Height).Should().Be(PauseAction.None);
    }
}
