using System.Numerics;
using FluentAssertions;
using Garupan.Content;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Spawn;
using Xunit;
using SimAmmo = Garupan.Sim.Components.AmmoType;
using CatalogAmmo = Garupan.Content.AmmoType;

namespace Garupan.Sim.Tests.Spawn;

/// <summary>
/// Verifies that <see cref="TankSpawner"/> reads the chambered round through
/// <see cref="AmmoCatalog"/> instead of constants, and that the Sim/Content AmmoType
/// mirror stays in lock-step so the cast in TankSpawner.ResolveChamberedRound is safe.
/// </summary>
public sealed class ChamberedRoundFromCatalogTests
{
    [Fact]
    public void Catalog_and_sim_ammo_types_share_numeric_values()
    {
        ((byte)CatalogAmmo.AP).Should().Be((byte)SimAmmo.AP);
        ((byte)CatalogAmmo.APCR).Should().Be((byte)SimAmmo.APCR);
        ((byte)CatalogAmmo.HEAT).Should().Be((byte)SimAmmo.HEAT);
        ((byte)CatalogAmmo.HE).Should().Be((byte)SimAmmo.HE);
        ((byte)CatalogAmmo.APC).Should().Be((byte)SimAmmo.APC);
        ((byte)CatalogAmmo.APCBC).Should().Be((byte)SimAmmo.APCBC);
        ((byte)CatalogAmmo.APHE).Should().Be((byte)SimAmmo.APHE);
        ((byte)CatalogAmmo.HVAP).Should().Be((byte)SimAmmo.HVAP);
    }

    [Fact]
    public void Pz_iv_g_pulls_pz_iv_75_ap_envelope_from_the_catalogue()
    {
        using var world = World.Create();

        var tank = TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumA,
            Vector2.Zero,
            0f,
            Team.PlayerSchool,
            TankControl.Player);

        var chambered = world.Get<Gun>(tank).Chambered;
        chambered.Type.Should().Be((SimAmmo)(byte)AmmoCatalog.AmmoMediumAAp.Type);
        chambered.MuzzleVelocityMps.Should().Be(AmmoCatalog.AmmoMediumAAp.MuzzleVelocityMps);
        chambered.MassKg.Should().Be(AmmoCatalog.AmmoMediumAAp.MassKg);
        chambered.DiameterMeters.Should().Be(AmmoCatalog.AmmoMediumAAp.DiameterMeters);
        chambered.DragCoefficient.Should().Be(AmmoCatalog.AmmoMediumAAp.DragCoefficient);
        ShouldMatchPenetrationCatalogue(chambered.Penetration, AmmoCatalog.AmmoMediumAAp.Id);
    }

    [Fact]
    public void HeavyA_uses_88mm_ap_envelope_distinct_from_the_pz_iv_round()
    {
        using var world = World.Create();

        var tank = TankSpawner.Spawn(
            world,
            TankRoster.VehicleHeavyA,
            Vector2.Zero,
            0f,
            Team.OpponentSchool,
            TankControl.AiBot);

        var chambered = world.Get<Gun>(tank).Chambered;
        chambered.MuzzleVelocityMps.Should().Be(AmmoCatalog.AmmoHeavyAAp.MuzzleVelocityMps);
        chambered.MassKg.Should().Be(AmmoCatalog.AmmoHeavyAAp.MassKg);
        chambered.MassKg.Should().NotBe(AmmoCatalog.AmmoMediumAAp.MassKg, "the HeavyA's 88 mm shell is heavier than the medium tank's 75 mm");
    }

    [Fact]
    public void MediumC_uses_76mm_ap_envelope()
    {
        using var world = World.Create();

        var tank = TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumC,
            Vector2.Zero,
            0f,
            Team.OpponentSchool,
            TankControl.AiBot);

        var chambered = world.Get<Gun>(tank).Chambered;
        chambered.MuzzleVelocityMps.Should().Be(AmmoCatalog.AmmoMediumCAp.MuzzleVelocityMps);
        ShouldMatchPenetrationCatalogue(chambered.Penetration, AmmoCatalog.AmmoMediumCAp.Id);
    }

    [Fact]
    public void Unknown_default_ammo_id_is_rejected_instead_of_borrowing_another_weapon_envelope()
    {
        using var world = World.Create();

        // Hand-built TankSpec with a deliberately bogus DefaultAmmoId. The spawn must
        // fail loudly instead of applying a plausible-looking shell from another gun.
        var bespoke = new TankSpec(
            Id:             "test_bogus",
            Designation:    "Test Bogus",
            DisplayNameKey: "tank.test_bogus",
            ModelResPath:   "res://content/tanks/none.glb",
            Armor:          new ArmorProfile(
                HullFront: new ArmorPlate(50, 0), HullSide: new ArmorPlate(30, 0), HullRear: new ArmorPlate(20, 0),
                TurretFront: new ArmorPlate(50, 0), TurretSide: new ArmorPlate(30, 0), TurretRear: new ArmorPlate(30, 0),
                Mantlet: new ArmorPlate(50, 0), Roof: new ArmorPlate(10, 90)),
            Gun:            new GunSpec(
                Id:              "test",
                Caliber:         "test",
                PenetrationMm:   77,
                Damage:          100,
                ReloadSeconds:   5,
                RoundsPerMinute: 10,
                DefaultAmmoId:   "no_such_round",
                RecoilingAssemblyMassKg: 900,
                MaximumRecoilTravelMeters: 0.5,
                RecoilBrakeForceNewtons: 100000,
                MuzzleBrakeEfficiency: 0,
                RecoilReturnSeconds: 0.55),
            GunMount:       GunMountCatalog.MountMediumA,
            Mobility:       new MobilitySpec(20, 300, 1000, 5, 2.5, 2.5, 20, GroundDriveCatalog.GermanMedium),
            CrewSize:       4);

        var act = () => TankSpawner.Spawn(world, bespoke, Vector2.Zero, 0f, Team.PlayerSchool, TankControl.None);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no_such_round*");
    }

    /// <summary>Asserts a baked round profile reproduces the published penetration table for its
    /// ammo id — the spawn pipeline copies the catalogue curve, it does not invent figures.</summary>
    private static void ShouldMatchPenetrationCatalogue(PenetrationProfile profile, string ammoId)
    {
        var curve = AmmoPenetrationCatalog.RequireById(ammoId);
        profile.Normal100Mm.Should().Be(curve.Normal100Mm);
        profile.Normal500Mm.Should().Be(curve.Normal500Mm);
        profile.Normal1000Mm.Should().Be(curve.Normal1000Mm);
    }
}
