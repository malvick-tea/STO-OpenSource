using FluentAssertions;
using Xunit;

namespace Garupan.Content.Tests;

/// <summary>
/// Extends the basic AmmoCatalogTests with assertions on the APCR / HEAT / HE rows
/// added when the catalogue grew beyond the three default AP rounds. Keeps the original
/// happy-path tests focused; this file holds the per-family invariants.
/// </summary>
public sealed class AmmoCatalogExpandedTests
{
    [Fact]
    public void Apcr_outpaces_its_ap_sibling_in_penetration_and_velocity()
    {
        AmmoCatalog.AmmoMediumAApcr.PenetrationMm.Should().BeGreaterThan(AmmoCatalog.AmmoMediumAAp.PenetrationMm);
        AmmoCatalog.AmmoMediumAApcr.MuzzleVelocityMps.Should().BeGreaterThan(AmmoCatalog.AmmoMediumAAp.MuzzleVelocityMps);
        AmmoCatalog.AmmoMediumAApcr.MassKg.Should().BeLessThan(
            AmmoCatalog.AmmoMediumAAp.MassKg,
            "sub-calibre rounds shed mass to push velocity / penetration up");
    }

    [Fact]
    public void HeavyA_apcr_outpenetrates_heavy_a_ap()
    {
        AmmoCatalog.AmmoHeavyAApcr.PenetrationMm.Should().BeGreaterThan(AmmoCatalog.AmmoHeavyAAp.PenetrationMm);
        AmmoCatalog.AmmoHeavyAApcr.Type.Should().Be(AmmoType.APCR);
    }

    [Fact]
    public void MediumC_hvap_closes_the_heavy_a_frontal_gap()
    {
        AmmoCatalog.AmmoMediumCHvap.PenetrationMm.Should().BeGreaterThan(
            100f,
            "the HVAP round's reason to exist is defeating the HeavyA's 100 mm frontal plate");
        AmmoCatalog.AmmoMediumCHvap.MuzzleVelocityMps.Should().BeGreaterThan(1000f);
    }

    [Fact]
    public void Heat_round_has_modest_penetration_but_distinct_family()
    {
        AmmoCatalog.AmmoMediumAHeat.Type.Should().Be(AmmoType.HEAT);
        AmmoCatalog.AmmoMediumAHeat.PenetrationMm.Should().BeGreaterThan(0f);
        AmmoCatalog.AmmoMediumAHeat.PenetrationMm.Should().BeLessThan(
            AmmoCatalog.AmmoMediumAAp.PenetrationMm,
            "HEAT trades raw penetration for range-independence");
    }

    [Fact]
    public void He_round_has_marginal_penetration_befitting_its_role()
    {
        AmmoCatalog.AmmoMediumAHe.Type.Should().Be(AmmoType.HE);
        AmmoCatalog.AmmoMediumAHe.PenetrationMm.Should().BeLessThan(
            20f,
            "HE shells are for soft targets, not armour");
    }

    [Fact]
    public void Every_ammo_in_the_roster_has_a_positive_muzzle_velocity()
    {
        foreach (var spec in AmmoCatalog.All)
        {
            spec.MuzzleVelocityMps.Should().BeGreaterThan(0f, $"{spec.Id} needs positive velocity");
            spec.MassKg.Should().BeGreaterThan(0f, $"{spec.Id} needs positive mass");
            spec.DiameterMeters.Should().BeGreaterThan(0f, $"{spec.Id} needs positive calibre geometry");
            spec.DragCoefficient.Should().BeGreaterThan(0f, $"{spec.Id} needs aerodynamic drag data");
        }
    }

    [Fact]
    public void New_rounds_resolve_through_find_by_id()
    {
        AmmoCatalog.FindById("ammo_medium_a_apcr").Should().Be(AmmoCatalog.AmmoMediumAApcr);
        AmmoCatalog.FindById("ammo_heavy_a_apcr").Should().Be(AmmoCatalog.AmmoHeavyAApcr);
        AmmoCatalog.FindById("ammo_medium_c_hvap").Should().Be(AmmoCatalog.AmmoMediumCHvap);
        AmmoCatalog.FindById("ammo_medium_a_heat").Should().Be(AmmoCatalog.AmmoMediumAHeat);
        AmmoCatalog.FindById("ammo_medium_a_he").Should().Be(AmmoCatalog.AmmoMediumAHe);
    }
}
