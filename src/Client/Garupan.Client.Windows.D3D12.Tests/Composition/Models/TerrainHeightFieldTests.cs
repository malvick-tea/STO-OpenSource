using System;
using System.IO;
using FluentAssertions;
using Garupan.Sim.Terrain;
using Xunit;

namespace Garupan.Client.Windows.Direct3D12.Tests.Composition.Models;

/// <summary>Integration check that the bundled Ardennes heightfield (the asset the D3D12 match
/// renderer seats tanks on) parses and carries real DEM relief. The pure parse/sample/normal
/// logic is covered headless in <c>Garupan.Sim.Tests</c>; this only guards the shipped blob.</summary>
public sealed class TerrainHeightFieldTests
{
    [Fact]
    public void Loads_the_bundled_ardennes_heightfield_with_real_relief()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "content", "maps", "ardennes.heightfield");
        File.Exists(path).Should().BeTrue($"the test output should bundle {path}");

        var field = TerrainHeightField.Load(File.ReadAllBytes(path));
        field.WorldSizeMeters.Should().BeApproximately(6000f, 1f);
        field.HeightAt(0f, 0f).Should().BeInRange(-200f, 200f);
        field.HeightAt(0f, 0f).Should().NotBe(field.HeightAt(1500f, -1200f), "the DEM surface is not flat");
    }
}
