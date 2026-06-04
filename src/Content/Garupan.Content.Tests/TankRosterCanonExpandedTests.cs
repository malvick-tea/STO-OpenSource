using FluentAssertions;
using Xunit;

namespace Garupan.Content.Tests;

/// <summary>
/// Coverage for the canon roster expansion: eight new vehicles layered onto the
/// original four (two medium tanks, a heavy tank, and a third medium tank). Bound the
/// per-vehicle invariants that are easy to break in a balance pass (the six-crew medium-D
/// config, the heavy-D howitzer on a different family, the medium-F topping the AP-pen
/// chart) so future edits fail loud instead of drifting silently.
/// </summary>
public sealed class TankRosterCanonExpandedTests
{
    [Fact]
    public void Roster_size_matches_the_canon_expansion()
    {
        // Twelve vehicles total across the light / medium / heavy / assault classes. If this
        // fails after an edit, either a new vehicle shipped without updating the roster docs
        // or one got dropped.
        TankRoster.All.Should().HaveCount(12);
    }

    [Theory]
    [InlineData("vehicle_light_a")]
    [InlineData("vehicle_assault_a")]
    [InlineData("vehicle_medium_d")]
    [InlineData("vehicle_heavy_b")]
    [InlineData("vehicle_heavy_c")]
    [InlineData("vehicle_medium_e")]
    [InlineData("vehicle_heavy_d")]
    [InlineData("vehicle_medium_f")]
    public void Canon_tank_resolves_through_find_by_id(string id)
    {
        TankRoster.FindById(id).Should().NotBeNull();
    }

    [Fact]
    public void Medium_d_carries_seven_crew_per_historical_configuration()
    {
        // Commander, 37 mm gunner + loader, 75 mm gunner + loader, driver, and radio operator.
        TankRoster.VehicleMediumD.CrewSize.Should().Be(7);
    }

    [Fact]
    public void Assault_a_has_zero_turret_traverse()
    {
        TankRoster.VehicleAssaultA.Mobility.TurretTraverseDegPerSec.Should().Be(
            0,
            "the AssaultGun is a casemate vehicle — the hull must turn to aim");
    }

    [Fact]
    public void HeavyC_has_the_thickest_frontal_armour()
    {
        // The heavy-C vehicle's 152 mm glacis is the roster's record.
        foreach (var tank in TankRoster.All)
        {
            if (tank.Id == "vehicle_heavy_c")
            {
                continue;
            }

            tank.Armor.HullFront.ThicknessMm.Should().BeLessOrEqualTo(
                TankRoster.VehicleHeavyC.Armor.HullFront.ThicknessMm,
                $"{tank.Id} should not exceed the HeavyC's frontal plate");
        }
    }

    [Fact]
    public void VehicleHeavyD_uses_the_152mm_howitzer_family()
    {
        TankRoster.VehicleHeavyD.Gun.Should().Be(GunCatalog.GunHeavyD);
        TankRoster.VehicleHeavyD.Gun.ReloadSeconds.Should().BeGreaterThan(
            20.0,
            "152mm howitzer reload should be in the 20-30s range — the trade-off for the round's lethality");
        TankRoster.VehicleHeavyD.Gun.DefaultAmmoId.Should().Be("ammo_heavy_d_he");
    }

    [Fact]
    public void MediumF_has_the_highest_default_round_penetration()
    {
        var medium_f = TankRoster.VehicleMediumF;
        var medium_fPen = AmmoCatalog.FindById(medium_f.Gun.DefaultAmmoId)!.PenetrationMm;

        foreach (var tank in TankRoster.All)
        {
            if (tank.Id == medium_f.Id)
            {
                continue;
            }

            var pen = AmmoCatalog.FindById(tank.Gun.DefaultAmmoId)!.PenetrationMm;
            pen.Should().BeLessOrEqualTo(
                medium_fPen,
                $"{tank.Id}'s default round should not out-penetrate the MediumF's");
        }
    }

    [Fact]
    public void VehicleMediumE_has_the_high_output_medium_powertrain()
    {
        TankRoster.VehicleMediumE.Mobility.EnginePowerHorsepower.Should().BeGreaterOrEqualTo(
            500,
            "medium tank E family canon mobility — RivalDelta's tactical doctrine relies on speed");
    }

    [Fact]
    public void Type_89B_is_the_lightest_and_slowest_canon_tank()
    {
        TankRoster.VehicleLightA.Armor.HullFront.ThicknessMm.Should().BeLessOrEqualTo(20);
        TankRoster.VehicleLightA.Mobility.MassTonnes.Should().BeLessThan(TankRoster.VehicleMediumA.Mobility.MassTonnes);
        TankRoster.VehicleLightA.Mobility.EnginePowerHorsepower.Should().BeLessThan(150);
    }

    [Fact]
    public void Char_B1_bis_primary_is_the_turret_47mm_anti_tank_gun()
    {
        // The 47 mm SA 35 in the APX4 turret was the B1 bis's real anti-tank weapon; the
        // hull 75 mm (gun_heavy_b) was a fixed howitzer aimed by steering the whole tank.
        TankRoster.VehicleHeavyB.Gun.Caliber.Should().Be("47mm SA 35");
        TankRoster.VehicleHeavyB.Gun.DefaultAmmoId.Should().Be("char_b1_47_ap");
    }

    [Fact]
    public void Every_canon_tank_resolves_a_gun_and_an_ammo_entry()
    {
        foreach (var tank in TankRoster.All)
        {
            GunCatalog.FindByCaliber(tank.Gun.Caliber).Should().NotBeNull(
                $"{tank.Id} must reference a gun in the catalogue (caliber '{tank.Gun.Caliber}')");
            AmmoCatalog.FindById(tank.Gun.DefaultAmmoId).Should().NotBeNull(
                $"{tank.Id} must reference an ammo id in the catalogue (id '{tank.Gun.DefaultAmmoId}')");
        }
    }
}
