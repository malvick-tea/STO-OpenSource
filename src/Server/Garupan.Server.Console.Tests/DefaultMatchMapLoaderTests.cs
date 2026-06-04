using System;
using System.IO;
using FluentAssertions;
using Garupan.Sim.Terrain;
using Xunit;

namespace Garupan.Server.Console.Tests;

public sealed class DefaultMatchMapLoaderTests : IDisposable
{
    private const string Catalog = """
        id,model,heightfield,props,obstacles
        city,city.glb,city.heightfield,city-props.csv,city-obstacles.csv
        ardennes,ardennes.glb,ardennes.heightfield,,
        """;

    private const string Props = """
        kind,x,z,yaw,base_diameter_m,height_m
        Tree,1,2,0,0.3,9
        """;

    private const string Obstacles = """
        x,z,yaw,half_w_m,half_d_m,height_m
        5,6,0,10,8,24
        """;

    private const int HeightFieldFormatVersion = 1;
    private const int FlatHeightFieldResolution = 2;
    private const float FlatWorldExtentMeters = 500f;
    private const float FlatGroundMeters = 0f;

    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public void Missing_catalog_keeps_server_on_flat_ground()
    {
        DefaultMatchMapLoader.TryLoad(_root).Should().BeNull();
    }

    [Fact]
    public void Bundled_catalog_prefers_the_procedural_japan_map()
    {
        var map = DefaultMatchMapLoader.TryLoad(AppContext.BaseDirectory);

        map.Should().NotBeNull("the server output bundles the procedural japan map ahead of the city + Ardennes fallback");
        map!.Id.Should().Be("japan");
        map.TerrainHeightSampler(0f, 0f).Should().Be(
            map.TerrainHeightSampler(300f, -200f),
            "the procedural japan map is flat ground");
    }

    [Fact]
    public void Bundled_ardennes_fallback_heightfield_retains_its_relief()
    {
        var ardennes = TerrainHeightField.Load(File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory, "content", "maps", "ardennes.heightfield")));

        ardennes.HeightAt(0f, 0f).Should().NotBe(
            ardennes.HeightAt(1500f, -1200f),
            "the bundled Ardennes fallback keeps its sampled relief even when the city shadows it");
    }

    [Fact]
    public void Complete_city_artifacts_take_priority_and_load_props_and_obstacles()
    {
        var maps = CreateMapDirectory();
        File.WriteAllText(Path.Combine(maps, "catalog.csv"), Catalog);
        File.WriteAllText(Path.Combine(maps, "city-props.csv"), Props);
        File.WriteAllText(Path.Combine(maps, "city-obstacles.csv"), Obstacles);
        WriteFlatHeightField(Path.Combine(maps, "city.heightfield"));

        var map = DefaultMatchMapLoader.TryLoad(_root);

        map!.Id.Should().Be("city");
        map.Props.Should().ContainSingle();
        map.Props[0].GroundPosition.X.Should().Be(1f);
        map.Obstacles.Should().ContainSingle();
        map.Obstacles[0].HalfWidthMeters.Should().Be(10f);
        map.TerrainHeightSampler(100f, -100f).Should().Be(FlatGroundMeters);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CreateMapDirectory()
    {
        var maps = Path.Combine(_root, "content", "maps");
        Directory.CreateDirectory(maps);
        return maps;
    }

    private static void WriteFlatHeightField(string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("GTHF"u8);
        writer.Write(HeightFieldFormatVersion);
        writer.Write(FlatHeightFieldResolution);
        writer.Write(FlatWorldExtentMeters);
        writer.Write(FlatGroundMeters);
        writer.Write(FlatGroundMeters);
        writer.Write(FlatGroundMeters);
        for (var i = 0; i < FlatHeightFieldResolution * FlatHeightFieldResolution; i++)
        {
            writer.Write(FlatGroundMeters);
        }
    }
}
