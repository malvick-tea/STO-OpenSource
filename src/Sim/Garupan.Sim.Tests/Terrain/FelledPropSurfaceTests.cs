using System.Numerics;
using FluentAssertions;
using Garupan.Sim.Components;
using Garupan.Sim.Terrain;
using Xunit;

namespace Garupan.Sim.Tests.Terrain;

public sealed class FelledPropSurfaceTests
{
    [Fact]
    public void A_fallen_member_peaks_at_one_diameter_over_its_axis()
    {
        var member = new FelledPropSurfaceMember(Vector2.Zero, FallYawRadians: 0f, LengthMeters: 9f, RadiusMeters: 0.15f, PropState.Fallen);

        // A point on the felled axis (the cylinder runs +X from the base): top = one diameter up.
        FelledPropSurface.HeightContribution(member, worldX: 4f, worldZ: 0f)
            .Should().BeApproximately(0.3f, 1e-4f, "a lying cylinder's top is one diameter above ground");
    }

    [Fact]
    public void Standing_and_broken_props_add_no_drive_over_height()
    {
        FelledPropSurface.HeightContribution(Member(PropState.Standing), 1f, 0f).Should().Be(0f);
        FelledPropSurface.HeightContribution(Member(PropState.Broken), 1f, 0f).Should().Be(0f);
    }

    [Fact]
    public void A_toppling_member_is_contactable_and_adds_height_over_its_axis()
    {
        FelledPropSurface.HeightContribution(Member(PropState.Toppling), 1f, 0f).Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Beyond_its_radius_the_member_adds_nothing()
    {
        // 0.2 m radius, sampled 1 m to the side of the axis — well clear of the lying cylinder.
        FelledPropSurface.HeightContribution(Member(PropState.Fallen), 1f, 1f).Should().Be(0f);
    }

    private static FelledPropSurfaceMember Member(PropState state) =>
        new(Vector2.Zero, FallYawRadians: 0f, LengthMeters: 8f, RadiusMeters: 0.2f, state);
}
