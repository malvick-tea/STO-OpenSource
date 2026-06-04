using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Maps;

public sealed class BattleMapCsvTests
{
    private const string Catalog = """
        id,model,heightfield,props,obstacles
        city,city.glb,city.heightfield,city-props.csv,
        ardennes,ardennes.glb,ardennes.heightfield,,
        """;

    [Fact]
    public void Render_resolution_falls_back_until_every_city_artifact_exists()
    {
        var catalog = BattleMapCsv.Parse(Catalog);
        var available = new[] { "city.glb", "city.heightfield", "ardennes.glb", "ardennes.heightfield" };

        var resolved = catalog.ResolveFirstRenderable(fileName => available.Contains(fileName, StringComparer.Ordinal));

        resolved!.Id.Should().Be("ardennes");
    }

    [Fact]
    public void Render_resolution_prefers_first_complete_candidate()
    {
        var catalog = BattleMapCsv.Parse(Catalog);
        var available = new[] { "city.glb", "city.heightfield", "city-props.csv" };

        var resolved = catalog.ResolveFirstRenderable(fileName => available.Contains(fileName, StringComparer.Ordinal));

        resolved!.Id.Should().Be("city");
    }

    [Fact]
    public void Authoritative_resolution_does_not_require_gpu_model()
    {
        var catalog = BattleMapCsv.Parse(Catalog);
        var available = new[] { "city.heightfield", "city-props.csv" };

        var resolved = catalog.ResolveFirstAuthoritative(fileName => available.Contains(fileName, StringComparer.Ordinal));

        resolved!.Id.Should().Be("city");
    }

    [Fact]
    public void Resolution_waits_for_a_declared_obstacles_file()
    {
        var catalog = BattleMapCsv.Parse("""
            id,model,heightfield,props,obstacles
            town,town.glb,town.heightfield,town-props.csv,town-obstacles.csv
            """);

        var withoutObstacles = new[] { "town.glb", "town.heightfield", "town-props.csv" };
        catalog.ResolveFirstRenderable(name => withoutObstacles.Contains(name, StringComparer.Ordinal))
            .Should().BeNull("a map that declares an obstacles table only activates once it exists");

        var complete = new[] { "town.glb", "town.heightfield", "town-props.csv", "town-obstacles.csv" };
        catalog.ResolveFirstRenderable(name => complete.Contains(name, StringComparer.Ordinal))!
            .ObstaclesFileName.Should().Be("town-obstacles.csv");
    }

    [Fact]
    public void Parser_rejects_parent_path_escape()
    {
        var act = () => BattleMapCsv.Parse("id,model,heightfield,props,obstacles\nbad,..\\bad.glb,bad.heightfield,,");

        act.Should().Throw<InvalidDataException>().WithMessage("*leaf file name*");
    }

    [Fact]
    public void Parser_rejects_duplicate_ids()
    {
        var act = () => BattleMapCsv.Parse("id,model,heightfield,props,obstacles\nsame,a.glb,a.heightfield,,\nsame,b.glb,b.heightfield,,");

        act.Should().Throw<InvalidDataException>().WithMessage("*duplicate id*");
    }
}
