using FluentAssertions;
using Garupan.Client.Ui.Navigation;
using Garupan.Client.Ui.Tests.Fixtures;
using Xunit;

namespace Garupan.Client.Ui.Tests.Navigation;

/// <summary>
/// Covers <see cref="ScreenStack.PopTo{TScreen}"/> — the primitive that collapses a
/// briefing → match → result sub-flow back to the campaign screen in one step.
/// </summary>
public sealed class ScreenStackPopToTests
{
    [Fact]
    public void PopTo_collapses_every_screen_above_the_target()
    {
        var stack = new ScreenStack();
        var target = new TargetScreen();
        stack.Replace(target, ScreenTransition.Instant);
        var f1 = Push(stack);
        var f2 = Push(stack);
        var f3 = Push(stack);

        var collapsed = stack.PopTo<TargetScreen>(ScreenTransition.Instant);

        collapsed.Should().BeTrue();
        stack.Current.Should().BeSameAs(target);
        stack.Depth.Should().Be(1);
        f1.OnExitCount.Should().Be(1);
        f2.OnExitCount.Should().Be(1);
        f3.OnExitCount.Should().Be(1);
        target.OnExitCount.Should().Be(0, "the target screen is revealed, not removed");
    }

    [Fact]
    public void PopTo_with_one_screen_above_target_pops_just_that_screen()
    {
        var stack = new ScreenStack();
        var target = new TargetScreen();
        stack.Replace(target, ScreenTransition.Instant);
        var filler = Push(stack);

        stack.PopTo<TargetScreen>(ScreenTransition.Instant).Should().BeTrue();

        stack.Current.Should().BeSameAs(target);
        filler.OnExitCount.Should().Be(1);
    }

    [Fact]
    public void PopTo_returns_false_when_target_type_is_not_on_the_stack()
    {
        var stack = new ScreenStack();
        stack.Replace(new FillerScreen(), ScreenTransition.Instant);
        Push(stack);

        var collapsed = stack.PopTo<TargetScreen>(ScreenTransition.Instant);

        collapsed.Should().BeFalse();
        stack.Depth.Should().Be(2, "a failed PopTo must leave the stack untouched");
    }

    [Fact]
    public void PopTo_returns_false_when_the_target_is_already_current()
    {
        var stack = new ScreenStack();
        stack.Replace(new FillerScreen(), ScreenTransition.Instant);
        var target = new TargetScreen();
        stack.Push(target, ScreenTransition.Instant);

        var collapsed = stack.PopTo<TargetScreen>(ScreenTransition.Instant);

        collapsed.Should().BeFalse();
        stack.Current.Should().BeSameAs(target);
        stack.Depth.Should().Be(2);
    }

    private static FillerScreen Push(ScreenStack stack)
    {
        var screen = new FillerScreen();
        stack.Push(screen, ScreenTransition.Instant);
        return screen;
    }
}
