using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Garupan.Sim.Terrain;
using Xunit;

namespace Garupan.Sim.Tests.Terrain;

public sealed class TerrainHeightFieldTests
{
    [Fact]
    public void Samples_grid_corners_exactly_and_centre_bilinearly()
    {
        var field = TerrainHeightField.Load(BuildBlob(2, 10f, new[] { 10f, 20f, 30f, 40f }));

        field.HeightAt(-5f, -5f).Should().BeApproximately(10f, 1e-4f);
        field.HeightAt(+5f, -5f).Should().BeApproximately(20f, 1e-4f);
        field.HeightAt(-5f, +5f).Should().BeApproximately(30f, 1e-4f);
        field.HeightAt(+5f, +5f).Should().BeApproximately(40f, 1e-4f);
        field.HeightAt(0f, 0f).Should().BeApproximately(25f, 1e-4f, "bilinear centre of the four corners");
    }

    [Fact]
    public void Clamps_positions_off_the_map_to_the_nearest_edge()
    {
        var field = TerrainHeightField.Load(BuildBlob(2, 10f, new[] { 10f, 20f, 30f, 40f }));

        field.HeightAt(-1000f, -1000f).Should().BeApproximately(10f, 1e-4f);
        field.HeightAt(+1000f, +1000f).Should().BeApproximately(40f, 1e-4f);
    }

    [Fact]
    public void Rejects_a_blob_without_the_GTHF_magic()
    {
        var act = () => TerrainHeightField.Load(new byte[64]);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Flat_ground_has_a_straight_up_normal()
    {
        var field = TerrainHeightField.Load(BuildBlob(3, 20f, Enumerable.Repeat(42f, 9).ToArray()));

        var normal = field.NormalAt(0f, 0f);
        normal.X.Should().BeApproximately(0f, 1e-5f);
        normal.Y.Should().BeApproximately(1f, 1e-5f);
        normal.Z.Should().BeApproximately(0f, 1e-5f);
    }

    [Fact]
    public void A_slope_rising_toward_east_tilts_the_normal_back_toward_west()
    {
        // A 3x3 ramp climbing +10 m per cell along +X (east), flat along Z. The upward normal of a
        // 45-degree east-facing slope is (-1, 1, 0)/sqrt(2): it leans away from the rise.
        var ramp = new[] { 0f, 10f, 20f, 0f, 10f, 20f, 0f, 10f, 20f };
        var field = TerrainHeightField.Load(BuildBlob(3, 20f, ramp));

        var normal = field.NormalAt(0f, 0f);
        normal.X.Should().BeApproximately(-MathF.Sqrt(0.5f), 1e-4f);
        normal.Y.Should().BeApproximately(MathF.Sqrt(0.5f), 1e-4f);
        normal.Z.Should().BeApproximately(0f, 1e-5f);
    }

    private static byte[] BuildBlob(int n, float world, float[] heights)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(new[] { (byte)'G', (byte)'T', (byte)'H', (byte)'F' });
        w.Write(1);
        w.Write(n);
        w.Write(world);
        w.Write(0f);
        w.Write(heights.Min());
        w.Write(heights.Max());
        foreach (var h in heights)
        {
            w.Write(h);
        }

        return ms.ToArray();
    }
}
