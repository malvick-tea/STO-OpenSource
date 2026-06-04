using FluentAssertions;
using Xunit;

namespace Garupan.Content.Tests;

public sealed class AmmoCatalogTests
{
    [Fact]
    public void Every_entry_has_a_unique_id()
    {
        var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var spec in AmmoCatalog.All)
        {
            ids.Add(spec.Id).Should().BeTrue($"id {spec.Id} should appear once");
        }
    }

    [Fact]
    public void FindById_returns_known_round()
    {
        var spec = AmmoCatalog.FindById("ammo_medium_a_ap");
        spec.Should().NotBeNull();
        spec!.Type.Should().Be(AmmoType.APCBC, "the medium gun's the AP shell was a capped, ballistic-capped shell");
        spec.PenetrationMm.Should().Be(135f);
    }

    [Fact]
    public void FindById_returns_null_for_unknown_id()
    {
        AmmoCatalog.FindById("definitely_not_a_round").Should().BeNull();
    }

    [Fact]
    public void Every_tank_in_roster_resolves_its_default_ammo_id()
    {
        foreach (var tank in TankRoster.All)
        {
            AmmoCatalog.FindById(tank.Gun.DefaultAmmoId).Should().NotBeNull(
                $"{tank.Id} references {tank.Gun.DefaultAmmoId} which must exist in the catalogue");
        }
    }
}
