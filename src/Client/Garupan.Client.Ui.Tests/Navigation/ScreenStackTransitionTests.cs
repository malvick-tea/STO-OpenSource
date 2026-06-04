using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Garupan.Client.Ui.Navigation;
using Garupan.Client.Ui.Tests.Fixtures;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;
using Xunit;

namespace Garupan.Client.Ui.Tests.Navigation;

/// <summary>
/// Covers <see cref="ScreenStack"/>'s fade-through-black transition: the outgoing screen
/// must darken into a full black cover before the incoming screen appears, so navigation
/// never hard-cuts straight to black or shows both screens stacked in one frame.
/// </summary>
public sealed class ScreenStackTransitionTests
{
    private static readonly FakeInputSource Input = new();

    [Fact]
    public void Render_without_a_transition_draws_the_current_screen_with_no_cover()
    {
        var stack = new ScreenStack();
        stack.Replace(new MarkerScreen("root"), ScreenTransition.Instant);

        var surface = Render(stack);

        DrawnScreens(surface).Should().Equal("root");
        BlackCover(surface).Should().BeNull();
    }

    [Fact]
    public void First_half_of_a_fade_shows_the_outgoing_screen_under_a_partial_black_cover()
    {
        var stack = StackMidFade("root", "next", elapsed: 0.25);

        var surface = Render(stack);

        DrawnScreens(surface).Should().Equal("root");
        BlackCover(surface)!.Value.A.Should().BeInRange(1, 254);
    }

    [Fact]
    public void Second_half_of_a_fade_shows_the_incoming_screen_under_a_lifting_black_cover()
    {
        var stack = StackMidFade("root", "next", elapsed: 0.75);

        var surface = Render(stack);

        DrawnScreens(surface).Should().Equal("next");
        BlackCover(surface)!.Value.A.Should().BeInRange(1, 254);
    }

    [Fact]
    public void A_fade_never_draws_both_screens_in_the_same_frame()
    {
        var stack = new ScreenStack();
        stack.Replace(new MarkerScreen("root"), ScreenTransition.Instant);
        stack.Push(new MarkerScreen("next"), ScreenTransition.Fade(1f));

        // Sweep the whole transition. Drawing the outgoing and incoming screens together
        // was the old hard-cut bug — exactly one screen must render per frame.
        for (var step = 0; step < 10; step++)
        {
            DrawnScreens(Render(stack)).Should().HaveCount(1);
            Advance(stack, 0.1);
        }
    }

    [Fact]
    public void A_completed_fade_drops_the_black_cover()
    {
        var stack = StackMidFade("root", "next", elapsed: 1.0);

        var surface = Render(stack);

        stack.IsTransitioning.Should().BeFalse();
        DrawnScreens(surface).Should().Equal("next");
        BlackCover(surface).Should().BeNull();
    }

    [Fact]
    public void A_pop_fade_reveals_the_screen_beneath_the_top()
    {
        var stack = new ScreenStack();
        stack.Replace(new MarkerScreen("root"), ScreenTransition.Instant);
        stack.Push(new MarkerScreen("top"), ScreenTransition.Instant);
        stack.Pop(ScreenTransition.Fade(1f));
        Advance(stack, 0.75);

        DrawnScreens(Render(stack)).Should().Equal("root");
    }

    private static ScreenStack StackMidFade(string from, string to, double elapsed)
    {
        var stack = new ScreenStack();
        stack.Replace(new MarkerScreen(from), ScreenTransition.Instant);
        stack.Push(new MarkerScreen(to), ScreenTransition.Fade(1f));
        Advance(stack, elapsed);
        return stack;
    }

    private static void Advance(ScreenStack stack, double seconds) =>
        stack.Update(new GameTime(Tick.Zero, seconds), Input);

    private static RecordingDrawSurface Render(ScreenStack stack)
    {
        var surface = new RecordingDrawSurface(800, 600);
        stack.Render(surface);
        return surface;
    }

    private static List<string> DrawnScreens(RecordingDrawSurface surface) =>
        surface.Commands.OfType<DrawTextCommand>()
            .Where(text => text.Text.StartsWith(MarkerScreen.Tag, System.StringComparison.Ordinal))
            .Select(text => text.Text[MarkerScreen.Tag.Length..])
            .ToList();

    private static Color? BlackCover(RecordingDrawSurface surface)
    {
        foreach (var command in surface.Commands)
        {
            if (command is DrawFillRect { X: 0, Y: 0, Color.R: 0, Color.G: 0, Color.B: 0 } rect &&
                rect.W == surface.Width && rect.H == surface.Height)
            {
                return rect.Color;
            }
        }

        return null;
    }

    /// <summary>Screen that stamps a recognisable tag so a test can tell which screen a
    /// frame rendered.</summary>
    private sealed class MarkerScreen : IScreen
    {
        public const string Tag = "screen:";

        private readonly string _id;

        public MarkerScreen(string id) => _id = id;

        public void OnEnter()
        {
        }

        public void OnExit()
        {
        }

        public void Update(GameTime time, IInputSource input)
        {
        }

        public void Render(IDrawSurface surface) =>
            surface.DrawText(Tag + _id, 0, 0, 12, new Color(255, 255, 255, 255));
    }
}
