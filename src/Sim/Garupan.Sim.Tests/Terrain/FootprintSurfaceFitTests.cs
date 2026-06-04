using System;
using System.Numerics;
using FluentAssertions;
using Garupan.Sim.Terrain;
using Xunit;

namespace Garupan.Sim.Tests.Terrain;

/// <summary>Footprint seating: a rigid hull leans only to the real gradient (never capsizes to a
/// point normal over a small pole) yet still <em>rides onto</em> a felled prop instead of letting its
/// tracks sink through it. The two headline guards are
/// <see cref="A_bump_under_one_corner_lifts_the_hull_onto_it_in_a_gentle_roll"/> (no capsize) and
/// <see cref="A_pole_across_the_belly_between_the_corners_lifts_the_whole_hull"/> (no clip-through).</summary>
public sealed class FootprintSurfaceFitTests
{
    private const float HalfLength = 3f;
    private const float HalfWidth = 1.45f;

    [Fact]
    public void Flat_ground_seats_the_hull_level_at_the_surface_height()
    {
        var (height, normal) = FootprintSurfaceFit.At(
            Surface((_, _) => 5f), worldX: 10f, worldZ: -4f, yawRadians: 0.7f, HalfLength, HalfWidth);

        height.Should().BeApproximately(5f, 1e-4f);
        normal.Y.Should().BeApproximately(1f, 1e-5f, "flat ground keeps the deck level");
    }

    [Fact]
    public void A_bump_under_one_corner_lifts_the_hull_onto_it_in_a_gentle_roll()
    {
        // A 0.2 m pole-sized spike under the front-right footprint corner (yaw 0). A point normal
        // would read near-vertical here and flip the hull; the seating must instead lift the hull
        // onto the bump (more than the bare 0.05 m corner mean) while staying nearly level.
        var surface = Surface((x, z) =>
            Vector2.Distance(new Vector2(x, z), new Vector2(HalfLength, HalfWidth)) < 0.15f ? 0.2f : 0f);

        var (height, normal) = FootprintSurfaceFit.At(surface, 0f, 0f, yawRadians: 0f, HalfLength, HalfWidth);

        height.Should().BeInRange(0.09f, 0.13f, "the hull climbs onto the bump rather than sinking the corner into it");
        normal.Y.Should().BeGreaterThan(0.99f, "a sub-footprint bump tilts the hull only a couple of degrees");
        UndersideHeightAt(height, normal, HalfLength, HalfWidth)
            .Should().BeGreaterThanOrEqualTo(0.2f - 1e-3f, "the lifted deck clears the bump it climbed onto, no clipping");
    }

    [Fact]
    public void A_pole_across_the_belly_between_the_corners_lifts_the_whole_hull()
    {
        // A pole lying ACROSS the hull's travel, right under the belly midline (x ~= 0). None of the
        // four corners (x = +/-3) touch it, so corner-only seating left the hull at 0 and the tracks
        // sank straight through. Sampling the track lines catches it and lifts the hull level onto it.
        var surface = Surface((x, _) => MathF.Abs(x) < 0.15f ? 0.3f : 0f);

        var (height, normal) = FootprintSurfaceFit.At(surface, 0f, 0f, yawRadians: 0f, HalfLength, HalfWidth);

        height.Should().BeApproximately(0.3f, 1e-3f, "the hull rides up onto the pole crossing under its belly");
        normal.Y.Should().BeApproximately(1f, 1e-3f, "a pole square under the midline lifts both tracks equally, no tilt");
    }

    [Fact]
    public void A_pole_between_the_tracks_does_not_lift_the_hull()
    {
        // A thin pole running along travel down the hull's centreline, between the two tracks. A real
        // chassis clears it on its belly; the seating must not lift the hull for it.
        var surface = Surface((_, z) => MathF.Abs(z) < 0.15f ? 0.3f : 0f);

        var (height, _) = FootprintSurfaceFit.At(surface, 0f, 0f, yawRadians: 0f, HalfLength, HalfWidth);

        height.Should().BeApproximately(0f, 1e-3f, "a pole between the tracks passes under the belly, lifting nothing");
    }

    [Fact]
    public void A_linear_slope_tilts_the_hull_to_the_true_gradient()
    {
        // Ground rising 0.1 m per metre toward +X; the up-normal leans back along -X by that slope.
        var (_, normal) = FootprintSurfaceFit.At(
            Surface((x, _) => x * 0.1f), 0f, 0f, yawRadians: 0f, HalfLength, HalfWidth);

        var expected = Vector3.Normalize(new Vector3(-0.1f, 1f, 0f));
        normal.X.Should().BeApproximately(expected.X, 1e-4f);
        normal.Y.Should().BeApproximately(expected.Y, 1e-4f);
        normal.Z.Should().BeApproximately(0f, 1e-4f);
    }

    /// <summary>The seated underside (a plane through the hull centre with the given up-normal)
    /// evaluated at the footprint corner offset (forward, right) from centre — used to assert the
    /// climbed-onto deck actually clears the obstacle under that corner.</summary>
    private static float UndersideHeightAt(float centreHeight, Vector3 normal, float along, float side) =>
        centreHeight - (((normal.X * along) + (normal.Z * side)) / normal.Y);

    private static IHeightSurface Surface(Func<float, float, float> heightAt) => new FuncSurface(heightAt);

    private sealed class FuncSurface : IHeightSurface
    {
        private readonly Func<float, float, float> _heightAt;

        public FuncSurface(Func<float, float, float> heightAt) => _heightAt = heightAt;

        public float HeightAt(float worldX, float worldZ) => _heightAt(worldX, worldZ);
    }
}
