using System;
using System.Numerics;
using FluentAssertions;
using Garupan.Content;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Spawn;
using Xunit;

namespace Garupan.Sim.Tests.Spawn;

public sealed class TankSpawnerTests
{
    [Fact]
    public void Spawn_attaches_full_component_set_for_an_ai_bot()
    {
        using var world = World.Create();

        var tank = TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumA,
            new Vector2(10f, -5f),
            0.5f,
            Team.OpponentSchool,
            TankControl.AiBot);

        world.Has<Transform>(tank).Should().BeTrue();
        world.Has<Hull>(tank).Should().BeTrue();
        world.Has<Turret>(tank).Should().BeTrue();
        world.Has<Gun>(tank).Should().BeTrue();
        world.Has<GunMount>(tank).Should().BeTrue();
        world.Has<HitRadius>(tank).Should().BeTrue();
        world.Has<TeamTag>(tank).Should().BeTrue();
        world.Has<DriveInput>(tank).Should().BeTrue();
        world.Has<TurretTarget>(tank).Should().BeTrue();
        world.Has<AiControlled>(tank).Should().BeTrue();
        world.Has<BotBrain>(tank).Should().BeTrue();
        world.Has<PlayerControlled>(tank).Should().BeFalse();
    }

    [Fact]
    public void Hit_radius_is_derived_per_chassis_instead_of_shared_across_the_roster()
    {
        using var world = World.Create();
        var pz = TankSpawner.Spawn(world, TankRoster.VehicleMediumA, Vector2.Zero, 0f, Team.PlayerSchool, TankControl.None);
        var heavy_a = TankSpawner.Spawn(world, TankRoster.VehicleHeavyA, Vector2.Zero, 0f, Team.OpponentSchool, TankControl.None);

        world.Get<HitRadius>(pz).Meters.Should().NotBe(world.Get<HitRadius>(heavy_a).Meters);
    }

    [Fact]
    public void Player_control_attaches_player_tag_not_bot_brain()
    {
        using var world = World.Create();

        var tank = TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumA,
            Vector2.Zero,
            0f,
            Team.PlayerSchool,
            TankControl.Player);

        world.Has<PlayerControlled>(tank).Should().BeTrue();
        world.Has<AiControlled>(tank).Should().BeFalse();
        world.Has<BotBrain>(tank).Should().BeFalse();
    }

    [Fact]
    public void None_control_leaves_the_entity_without_a_controller_tag()
    {
        using var world = World.Create();

        var tank = TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumA,
            Vector2.Zero,
            0f,
            Team.None,
            TankControl.None);

        world.Has<PlayerControlled>(tank).Should().BeFalse();
        world.Has<AiControlled>(tank).Should().BeFalse();
        world.Has<BotBrain>(tank).Should().BeFalse();
        // Sim components still attached — the tank is targetable, just driverless.
        world.Has<Hull>(tank).Should().BeTrue();
    }

    [Fact]
    public void Mobility_builds_force_based_dynamics_from_physical_spec()
    {
        using var world = World.Create();

        var tank = TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumA,
            Vector2.Zero,
            0f,
            Team.PlayerSchool,
            TankControl.Player);

        var hull = world.Get<Hull>(tank);
        hull.Dynamics.Should().NotBeNull();
        hull.Dynamics!.MassKg.Should().BeApproximately(23600f, 0.001f);
        hull.Dynamics.Powertrain.MaximumPowerWatts.Should().BeGreaterThan(0f);
        hull.DynamicsState.VelocityMps.Should().Be(Vector2.Zero);

        var turret = world.Get<Turret>(tank);
        turret.TraverseSpeedRadPerS.Should().BeApproximately(14f * (MathF.PI / 180f), 0.001f);
        turret.YawRadians.Should().Be(0f);
    }

    [Fact]
    public void Gun_starts_in_full_cooldown_so_the_first_shot_pays_the_reload()
    {
        using var world = World.Create();

        var tank = TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumA, // ReloadSeconds=3.4
            Vector2.Zero,
            0f,
            Team.PlayerSchool,
            TankControl.Player);

        var gun = world.Get<Gun>(tank);
        gun.ReloadSecondsMax.Should().BeApproximately(3.4f, 0.001f);
        gun.ReloadSeconds.Should().Be(
            gun.ReloadSecondsMax,
            "the spawner primes the reload clock so the first shot has the same delay as later shots");
        var expectedCurve = AmmoPenetrationCatalog.RequireById(AmmoCatalog.AmmoMediumAAp.Id);
        gun.Chambered.Penetration.Normal500Mm.Should().Be(expectedCurve.Normal500Mm);
    }

    [Fact]
    public void Armor_map_mirrors_spec_armor_profile()
    {
        using var world = World.Create();

        var tank = TankSpawner.Spawn(
            world,
            TankRoster.VehicleHeavyA, // FrontMm=100, SideMm=80, RearMm=80
            Vector2.Zero,
            0f,
            Team.OpponentSchool,
            TankControl.AiBot);

        var hull = world.Get<Hull>(tank);
        hull.Armor.HullFrontMm.Should().Be(100f);
        hull.Armor.HullSideMm.Should().Be(80f);
        hull.Armor.HullRearMm.Should().Be(80f);
        // Phase-0 catalogue doesn't split hull/turret armour, so the turret legs share values.
        hull.Armor.TurretFrontMm.Should().Be(100f);
        hull.Armor.TurretSideMm.Should().Be(80f);
        hull.Armor.TurretRearMm.Should().Be(80f);
    }

    [Fact]
    public void Custom_bot_brain_overrides_the_default_engage_range()
    {
        using var world = World.Create();

        var tank = TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumA,
            Vector2.Zero,
            0f,
            Team.OpponentSchool,
            TankControl.AiBot,
            botBrain: new BotBrain { EngageRangeMeters = 120f });

        world.Get<BotBrain>(tank).EngageRangeMeters.Should().Be(120f);
    }

    [Fact]
    public void Default_bot_brain_uses_the_phase_zero_engage_range()
    {
        using var world = World.Create();

        var tank = TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumA,
            Vector2.Zero,
            0f,
            Team.OpponentSchool,
            TankControl.AiBot);

        world.Get<BotBrain>(tank).EngageRangeMeters.Should().Be(TankSpawner.DefaultBotEngageRangeMeters);
    }

    [Fact]
    public void Transform_carries_position_and_yaw_from_arguments()
    {
        using var world = World.Create();

        var tank = TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumA,
            new Vector2(12.5f, -7.25f),
            1.25f,
            Team.PlayerSchool,
            TankControl.Player);

        var tf = world.Get<Transform>(tank);
        tf.Position.Should().Be(new Vector2(12.5f, -7.25f));
        tf.YawRadians.Should().Be(1.25f);
        world.Get<Turret>(tank).YawRadians.Should().Be(1.25f);
    }

    [Fact]
    public void Network_id_argument_stamps_the_replication_identity_component()
    {
        using var world = World.Create();

        var tank = TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumA,
            Vector2.Zero,
            0f,
            Team.PlayerSchool,
            TankControl.None,
            networkId: new NetworkId(42u));

        world.Has<NetworkId>(tank).Should().BeTrue();
        world.Get<NetworkId>(tank).Value.Should().Be(42u);
    }

    [Fact]
    public void Omitting_the_network_id_leaves_the_tank_without_a_replication_identity()
    {
        using var world = World.Create();

        var tank = TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumA,
            Vector2.Zero,
            0f,
            Team.PlayerSchool,
            TankControl.Player);

        world.Has<NetworkId>(tank).Should().BeFalse(
            "single-player / determinism spawns must not carry a server-assigned network identity");
    }
}
