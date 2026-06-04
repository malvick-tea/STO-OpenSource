using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Maps;

public sealed class PropCatalogCsvTests
{
    private const string MaterialHeader =
        "id,modulus_of_rupture_pa,failure_deflection_radians,density_kg_per_cubic_meter";

    [Fact]
    public void Embedded_catalog_resolves_tree_physics_from_csv()
    {
        var tree = PropKindCatalog.For(PropKind.Tree);

        tree.Behavior.Should().Be(PropBehavior.Topple);
        tree.Material.Name.Should().Be("green_wood");
        tree.Material.ModulusOfRupturePa.Should().Be(40000000f);
    }

    [Fact]
    public void Material_parser_rejects_non_finite_values()
    {
        var act = () => PropMaterialCsv.Parse($"{MaterialHeader}\nwood,NaN,0.05,700");

        act.Should().Throw<InvalidDataException>().WithMessage("*finite positive*");
    }

    [Fact]
    public void Kind_parser_rejects_missing_enum_rows()
    {
        var materials = PropMaterialCsv.Parse($"{MaterialHeader}\nwood,40000000,0.05,700");
        var act = () => PropKindCsv.Parse(
            "kind,behavior,material_id\nTree,Topple,wood",
            id => materials.TryGetValue(id, out var material)
                ? material
                : throw new KeyNotFoundException());

        act.Should().Throw<InvalidDataException>().WithMessage("*no row*");
    }
}
