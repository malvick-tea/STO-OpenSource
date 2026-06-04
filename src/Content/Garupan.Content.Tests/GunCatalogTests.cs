using FluentAssertions;
using Xunit;

namespace Garupan.Content.Tests;

public sealed class GunCatalogTests
{
    [Fact]
    public void Every_canonical_gun_has_a_known_default_ammo_id()
    {
        foreach (var gun in GunCatalog.All)
        {
            AmmoCatalog.FindById(gun.DefaultAmmoId).Should().NotBeNull(
                $"gun '{gun.Caliber}' references ammo id '{gun.DefaultAmmoId}'");
        }
    }

    [Fact]
    public void Find_by_caliber_resolves_known_entries()
    {
        GunCatalog.FindByCaliber("7.5cm medium gun").Should().Be(GunCatalog.GunMediumA);
        GunCatalog.FindByCaliber("8.8cm heavy gun").Should().Be(GunCatalog.GunHeavyA);
        GunCatalog.FindByCaliber("76mm medium gun").Should().Be(GunCatalog.GunMediumC);
    }

    [Fact]
    public void Find_by_caliber_returns_null_for_unknown_caliber()
    {
        GunCatalog.FindByCaliber("definitely not a gun").Should().BeNull();
    }

    [Fact]
    public void Medium_a_gun_overrides_only_reload_and_rpm_from_canonical_gun()
    {
        var canonical = GunCatalog.GunMediumA;
        var rosterGun = TankRoster.VehicleMediumA.Gun;

        rosterGun.Caliber.Should().Be(canonical.Caliber);
        rosterGun.PenetrationMm.Should().Be(canonical.PenetrationMm);
        rosterGun.DefaultAmmoId.Should().Be(canonical.DefaultAmmoId);
        rosterGun.ReloadSeconds.Should().NotBe(canonical.ReloadSeconds, "this vehicle has a faster loader");
    }

    [Fact]
    public void Medium_b_is_the_player_tank_with_the_faster_loader()
    {
        // The medium-B vehicle is the player's tank — it carries the full medium-gun
        // penetration envelope and the trained-crew faster loader, not a derate.
        var canonical = GunCatalog.GunMediumA;
        var rosterGun = TankRoster.VehicleMediumB.Gun;

        rosterGun.Caliber.Should().Be(canonical.Caliber);
        rosterGun.DefaultAmmoId.Should().Be(canonical.DefaultAmmoId);
        rosterGun.PenetrationMm.Should().Be(canonical.PenetrationMm, "this variant is not derated");
        rosterGun.ReloadSeconds.Should().NotBe(canonical.ReloadSeconds, "the player's tank carries the faster loader");
        TankRoster.VehicleMediumB.School.Should().Be(OpponentSchool.PlayerSchool, "this is the player's tank");
    }

    [Fact]
    public void HeavyA_and_medium_c_share_canonical_envelope_unchanged()
    {
        TankRoster.VehicleHeavyA.Gun.Should().Be(GunCatalog.GunHeavyA);
        TankRoster.VehicleMediumC.Gun.Should().Be(GunCatalog.GunMediumC);
    }
}
