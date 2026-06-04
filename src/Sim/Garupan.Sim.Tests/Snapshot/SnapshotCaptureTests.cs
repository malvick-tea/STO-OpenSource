using System.Linq;
using System.Numerics;
using Arch.Core;
using FluentAssertions;
using Garupan.Content;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Snapshot;
using Garupan.Sim.Spawn;
using Opus.Foundation;
using Xunit;
using AmmoType = Garupan.Sim.Components.AmmoType;

namespace Garupan.Sim.Tests.Snapshot;

public sealed class SnapshotCaptureTests
{
    [Fact]
    public void Empty_world_captures_an_empty_snapshot()
    {
        using var world = World.Create();

        var snap = SnapshotCapture.Capture(world, new Tick(0));

        snap.Tick.Should().Be(new Tick(0));
        snap.Entities.Should().BeEmpty();
        snap.Projectiles.Should().BeEmpty();
    }

    [Fact]
    public void Tanks_become_entity_rows_with_pose_and_turret_yaw()
    {
        using var world = World.Create();

        TankSpawner.Spawn(world, TankRoster.VehicleMediumA, new Vector2(10f, -5f), 1.5f, Team.PlayerSchool, TankControl.Player);
        TankSpawner.Spawn(world, TankRoster.VehicleHeavyA, new Vector2(-8f, 12f), -0.25f, Team.OpponentSchool, TankControl.AiBot);

        var snap = SnapshotCapture.Capture(world, new Tick(42));

        snap.Tick.Should().Be(new Tick(42));
        snap.Entities.Should().HaveCount(2);

        var player = snap.Entities.Single(e => e.Position == new Vector2(10f, -5f));
        player.YawRadians.Should().Be(1.5f);
        player.TurretYawRadians.Should().Be(1.5f);
        player.MinBarrelPitchRadians.Should().BeApproximately(-10f * MathF.PI / 180f, 1e-5f);
        player.MaxBarrelPitchRadians.Should().BeApproximately(20.05f * MathF.PI / 180f, 1e-5f);
        player.StateFlags.Should().Be(EntityStateFlags.None);

        var heavy_a = snap.Entities.Single(e => e.Position == new Vector2(-8f, 12f));
        heavy_a.YawRadians.Should().Be(-0.25f);
        heavy_a.StateFlags.Should().Be(EntityStateFlags.None);
    }

    [Fact]
    public void Knocked_out_tanks_set_the_knocked_out_flag_but_stay_in_snapshot()
    {
        using var world = World.Create();

        var tank = TankSpawner.Spawn(world, TankRoster.VehicleMediumA, Vector2.Zero, 0f, Team.PlayerSchool, TankControl.Player);
        world.Add(tank, default(KnockedOut));

        var snap = SnapshotCapture.Capture(world, new Tick(1));

        snap.Entities.Should().HaveCount(1);
        snap.Entities[0].StateFlags.Should().Be(EntityStateFlags.KnockedOut);
    }

    [Fact]
    public void Projectiles_become_projectile_rows_with_velocity_and_family()
    {
        using var world = World.Create();

        var p = world.Spawn(
            new Transform(new Vector2(3f, 4f), 0f),
            new Projectile
            {
                Velocity = new Vector2(100f, 0f),
                VisualHeightMeters = 2.25f,
                VerticalVelocityMps = 3.5f,
                DistanceTravelledMeters = 42f,
                LaunchPosition = new Vector2(1f, 2f),
                LaunchVisualHeightMeters = 1.75f,
                Type = AmmoType.APCR,
                Penetration = PenetrationProfile.Flat(150f),
                MassKg = 5.5f,
            });
        _ = p;

        var snap = SnapshotCapture.Capture(world, new Tick(7));

        snap.Entities.Should().BeEmpty();
        snap.Projectiles.Should().HaveCount(1);

        var row = snap.Projectiles[0];
        row.Position.Should().Be(new Vector2(3f, 4f));
        row.Velocity.Should().Be(new Vector2(100f, 0f));
        row.Family.Should().Be(AmmoType.APCR);
        row.VisualHeightMeters.Should().Be(2.25f);
        row.VerticalVelocityMps.Should().Be(3.5f);
        row.DistanceTravelledMeters.Should().Be(42f);
        row.LaunchPosition.Should().Be(new Vector2(1f, 2f));
        row.LaunchVisualHeightMeters.Should().Be(1.75f);
    }

    [Fact]
    public void Tanks_and_projectiles_partition_into_separate_lists()
    {
        using var world = World.Create();

        TankSpawner.Spawn(world, TankRoster.VehicleMediumA, Vector2.Zero, 0f, Team.PlayerSchool, TankControl.Player);
        world.Spawn(
            new Transform(new Vector2(20f, 0f), 0f),
            new Projectile { Velocity = new Vector2(500f, 0f), Type = AmmoType.AP });

        var snap = SnapshotCapture.Capture(world, new Tick(99));

        snap.Entities.Should().HaveCount(1, "the tank is in the entity section, not the projectile section");
        snap.Projectiles.Should().HaveCount(1, "the projectile is in the projectile section, not the entity section");
    }

    [Fact]
    public void Tank_recoil_travel_and_projectile_owner_network_id_are_captured()
    {
        using var world = World.Create();
        var owner = TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumA,
            Vector2.Zero,
            0f,
            Team.PlayerSchool,
            TankControl.None,
            networkId: new NetworkId { Value = 77 });
        world.Add(owner, new GunRecoilState { TravelMeters = 0.25f });
        var projectile = world.Spawn(
            new Transform(Vector2.One, 0f),
            new Projectile { Velocity = Vector2.UnitX });
        world.Add(projectile, new Owner { Entity = owner });

        var snap = SnapshotCapture.Capture(world, new Tick(1));

        snap.Entities.Should().ContainSingle().Which.GunRecoilTravelMeters.Should().Be(0.25f);
        snap.Projectiles.Should().ContainSingle().Which.OwnerEntityId.Should().Be(77);
    }

    [Fact]
    public void Tick_field_round_trips_the_capture_argument()
    {
        using var world = World.Create();

        var snap = SnapshotCapture.Capture(world, new Tick(1234));

        snap.Tick.Should().Be(new Tick(1234));
    }

    [Fact]
    public void Standing_props_are_omitted_so_the_section_carries_only_the_felled()
    {
        using var world = World.Create();
        MapPropSpawner.Spawn(world, new[]
        {
            new MapProp(PropKind.LampPost, new Vector2(5f, 0f), 0f, 0.18f, 9f),     // id 0 — stays standing
            new MapProp(PropKind.TrafficSign, new Vector2(0f, 5f), 0f, 0.08f, 2.5f), // id 1 — felled below
            new MapProp(PropKind.Bin, new Vector2(-5f, 0f), 0f, 0.4f, 1f),           // id 2 — stays standing
        });
        FellProp(world, propId: 1, PropState.Toppling, fallYaw: 1.2f, seconds: 0.3f);

        var snap = SnapshotCapture.Capture(world, new Tick(5));

        snap.Entities.Should().BeEmpty("props are not tanks");
        var felled = snap.Props.Should().ContainSingle().Subject;
        felled.PropId.Should().Be(1);
        felled.State.Should().Be(PropState.Toppling);
        felled.FallYawRadians.Should().Be(1.2f);
        felled.ToppleSeconds.Should().Be(0.3f);
    }

    [Fact]
    public void An_all_standing_world_captures_no_props()
    {
        using var world = World.Create();
        MapPropSpawner.Spawn(world, new[]
        {
            new MapProp(PropKind.LampPost, new Vector2(5f, 0f), 0f, 0.18f, 9f),
            new MapProp(PropKind.LampPost, new Vector2(7f, 0f), 0f, 0.18f, 9f),
        });

        var snap = SnapshotCapture.Capture(world, new Tick(1));

        snap.Props.Should().BeEmpty();
    }

    /// <summary>Mutates the single prop with <paramref name="propId"/> into a felled state, the way
    /// <see cref="Garupan.Sim.Systems.PropCollisionSystem"/> would on a defeating hull contact.</summary>
    private static void FellProp(World world, int propId, PropState state, float fallYaw, float seconds)
    {
        var query = new QueryDescription().WithAll<DestructibleProp>();
        world.Raw.Query(in query, (ref DestructibleProp prop) =>
        {
            if (prop.PropId == propId)
            {
                prop.State = state;
                prop.FallYawRadians = fallYaw;
                prop.StateSeconds = seconds;
            }
        });
    }
}
