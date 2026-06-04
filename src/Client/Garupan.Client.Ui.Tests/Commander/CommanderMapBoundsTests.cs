using FluentAssertions;
using Garupan.Client.Ui.Commander;
using Xunit;

namespace Garupan.Client.Ui.Tests.Commander;

/// <summary>Covers <see cref="CommanderMapBounds.Contains"/> — the half-open rectangle.</summary>
public sealed class CommanderMapBoundsTests
{
    private static readonly CommanderMapBounds Bounds = new(X: 100, Y: 50, Width: 200, Height: 100);

    [Fact]
    public void A_point_inside_the_rectangle_is_contained()
    {
        Bounds.Contains(150, 80).Should().BeTrue();
    }

    [Fact]
    public void The_top_left_corner_is_inclusive()
    {
        Bounds.Contains(100, 50).Should().BeTrue();
    }

    [Fact]
    public void The_bottom_right_corner_is_exclusive()
    {
        Bounds.Contains(300, 150).Should().BeFalse(
            "half-open ranges keep adjacent regions from bleeding into the map");
    }

    [Fact]
    public void Points_outside_the_rectangle_are_not_contained()
    {
        Bounds.Contains(99, 80).Should().BeFalse();
        Bounds.Contains(150, 49).Should().BeFalse();
        Bounds.Contains(301, 80).Should().BeFalse();
        Bounds.Contains(150, 200).Should().BeFalse();
    }
}
