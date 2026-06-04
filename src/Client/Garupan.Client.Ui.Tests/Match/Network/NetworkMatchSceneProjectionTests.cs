using System;
using System.Numerics;
using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Garupan.Sim.Snapshot;
using Opus.Engine.Ui;
using Opus.Foundation;
using Xunit;

namespace Garupan.Client.Ui.Tests.Match.Network;

/// <summary>
/// Pure projection coverage for <see cref="NetworkMatchSceneProjection"/>: chase-camera
/// framing off the local tank, the world → ground-plane placement mapping, the self /
/// knocked-out flags, and the overview fallback when the local tank is absent. Each test
/// is a contract sentence; no GPU, no ECS.
/// </summary>
public sealed class NetworkMatchSceneProjectionTests
{
    private const uint LocalId = 7u;
    private const float Tolerance = 1e-3f;

    [Fact]
    public void Null_snapshot_yields_the_overview_camera_and_no_tanks()
    {
        var plan = NetworkMatchSceneProjection.Build(null, LocalId);

        plan.Tanks.Should().BeEmpty();
        AssertOverview(plan.Camera);
    }

    [Fact]
    public void Empty_snapshot_yields_the_overview_camera_and_no_tanks()
    {
        var plan = NetworkMatchSceneProjection.Build(Snapshot(), LocalId);

        plan.Tanks.Should().BeEmpty();
        AssertOverview(plan.Camera);
    }

    [Fact]
    public void Every_entity_becomes_a_tank_placement_on_the_ground_plane()
    {
        var plan = NetworkMatchSceneProjection.Build(
            Snapshot(Entity(LocalId, new Vector2(3f, 5f)), Entity(2, new Vector2(-4f, 8f))),
            LocalId);

        plan.Tanks.Should().HaveCount(2);
        plan.Tanks[0].Position.Should().Be(new Vector3(3f, 0f, 5f));
        plan.Tanks[1].Position.Should().Be(new Vector3(-4f, 0f, 8f));
    }

    [Fact]
    public void Tank_placement_carries_snapshot_tick_entity_id_and_world_turret_yaw()
    {
        var snapshot = new WorldSnapshot(
            new Tick(42),
            new[] { Entity(LocalId, Vector2.Zero) with { TurretYawRadians = 0.75f, GunRecoilTravelMeters = 0.25f } },
            Array.Empty<ProjectileSnapshot>());

        var plan = NetworkMatchSceneProjection.Build(snapshot, LocalId);

        plan.SnapshotTick.Should().Be(42);
        plan.Tanks[0].EntityId.Should().Be((int)LocalId);
        plan.Tanks[0].TurretYawRadians.Should().Be(0.75f);
        plan.Tanks[0].GunRecoilTravelMeters.Should().Be(0.25f);
    }

    [Fact]
    public void Local_barrel_pitch_is_applied_only_to_the_players_tank()
    {
        var plan = NetworkMatchSceneProjection.Build(
            Snapshot(Entity(LocalId, Vector2.Zero), Entity(2, Vector2.One)),
            LocalId,
            localBarrelPitchRadians: 0.2f);

        plan.Tanks[0].BarrelPitchRadians.Should().Be(0.2f);
        plan.Tanks[1].BarrelPitchRadians.Should().Be(0f);
    }

    [Fact]
    public void Local_barrel_preview_is_clamped_to_the_players_gun_mount_limits()
    {
        var plan = NetworkMatchSceneProjection.Build(
            Snapshot(Entity(LocalId, Vector2.Zero) with
            {
                MinBarrelPitchRadians = -0.1f,
                MaxBarrelPitchRadians = 0.2f,
            }),
            LocalId,
            localBarrelPitchRadians: 1f);

        plan.Tanks[0].BarrelPitchRadians.Should().Be(0.2f);
    }

    [Fact]
    public void The_local_tank_is_flagged_and_others_are_not()
    {
        var plan = NetworkMatchSceneProjection.Build(
            Snapshot(Entity(2, Vector2.Zero), Entity(LocalId, Vector2.Zero)),
            LocalId);

        plan.Tanks[0].IsSelf.Should().BeFalse();
        plan.Tanks[1].IsSelf.Should().BeTrue();
    }

    [Fact]
    public void Snapshot_barrel_pitch_is_applied_to_remote_tanks()
    {
        var plan = NetworkMatchSceneProjection.Build(
            Snapshot(Entity(2, Vector2.Zero) with { BarrelPitchRadians = 0.15f }),
            LocalId);

        plan.Tanks[0].BarrelPitchRadians.Should().Be(0.15f);
    }

    [Fact]
    public void A_knocked_out_entity_is_flagged()
    {
        var plan = NetworkMatchSceneProjection.Build(
            Snapshot(Entity(LocalId, Vector2.Zero, flags: EntityStateFlags.KnockedOut)),
            LocalId);

        plan.Tanks[0].KnockedOut.Should().BeTrue();
    }

    [Fact]
    public void The_orbit_camera_sits_on_a_sphere_around_the_local_tank()
    {
        // Orbit yaw 0 + pitch 0: the camera is due +X (east) of the tank at ground level,
        // at the orbit distance, aiming just above the hull.
        var plan = NetworkMatchSceneProjection.Build(
            Snapshot(Entity(LocalId, Vector2.Zero)),
            LocalId,
            orbitYawRadians: 0f,
            orbitPitchRadians: 0f);

        AssertVector(plan.Camera.Position, new Vector3(NetworkMatchSceneProjection.ChaseDistanceMeters, 0f, 0f));
        AssertVector(plan.Camera.Target, new Vector3(0f, NetworkMatchSceneProjection.TargetHeightMeters, 0f));
        plan.Camera.FovYDegrees.Should().Be(NetworkMatchSceneProjection.FovYDegrees);
    }

    [Fact]
    public void The_orbit_camera_follows_the_tank_position_but_not_its_heading()
    {
        // Same orbit, two different hull yaws: the camera is identical (decoupled from
        // steering) and tracks the tank's world position — the fix for "camera swings on WASD".
        var plan1 = NetworkMatchSceneProjection.Build(
            Snapshot(Entity(LocalId, new Vector2(10f, 5f), yaw: 0f)), LocalId,
            orbitYawRadians: 0f, orbitPitchRadians: 0f);
        var plan2 = NetworkMatchSceneProjection.Build(
            Snapshot(Entity(LocalId, new Vector2(10f, 5f), yaw: MathF.PI)), LocalId,
            orbitYawRadians: 0f, orbitPitchRadians: 0f);

        AssertVector(plan1.Camera.Position, new Vector3(10f + NetworkMatchSceneProjection.ChaseDistanceMeters, 0f, 5f));
        AssertVector(plan2.Camera.Position, plan1.Camera.Position);
        AssertVector(plan1.Camera.Target, new Vector3(10f, NetworkMatchSceneProjection.TargetHeightMeters, 5f));
    }

    [Fact]
    public void The_orbit_camera_uses_the_requested_distance_inside_the_zoom_range()
    {
        var plan = NetworkMatchSceneProjection.Build(
            Snapshot(Entity(LocalId, Vector2.Zero)),
            LocalId,
            orbitYawRadians: 0f,
            orbitPitchRadians: 0f,
            chaseDistanceMeters: 9f);

        AssertVector(plan.Camera.Position, new Vector3(9f, 0f, 0f));
    }

    [Fact]
    public void The_orbit_camera_clamps_excessive_zoom_out()
    {
        var plan = NetworkMatchSceneProjection.Build(
            Snapshot(Entity(LocalId, Vector2.Zero)),
            LocalId,
            orbitYawRadians: 0f,
            orbitPitchRadians: 0f,
            chaseDistanceMeters: 100f);

        AssertVector(plan.Camera.Position, new Vector3(NetworkMatchSceneProjection.MaxChaseDistanceMeters, 0f, 0f));
    }

    [Fact]
    public void Without_a_local_tank_the_overview_frames_the_field_but_tanks_still_render()
    {
        // localId not present in the snapshot — e.g. before the first server broadcast.
        var plan = NetworkMatchSceneProjection.Build(Snapshot(Entity(2, new Vector2(1f, 1f))), LocalId);

        plan.Tanks.Should().HaveCount(1);
        plan.Tanks[0].IsSelf.Should().BeFalse();
        AssertOverview(plan.Camera);
    }

    [Fact]
    public void A_zero_local_id_never_matches_an_entity()
    {
        // 0 is the "not networked" sentinel — even an entity with Id 0 is not self.
        var plan = NetworkMatchSceneProjection.Build(Snapshot(Entity(0, Vector2.Zero)), localNetworkId: 0u);

        plan.Tanks[0].IsSelf.Should().BeFalse();
        AssertOverview(plan.Camera);
    }

    [Fact]
    public void Projectiles_map_to_ground_plane_position_and_velocity()
    {
        var snapshot = new WorldSnapshot(
            Tick.Zero,
            new[] { Entity(LocalId, Vector2.Zero) },
            new[]
            {
                new ProjectileSnapshot(
                    1,
                    new Vector2(5f, 7f),
                    new Vector2(3f, 4f),
                    default,
                    2.25f,
                    6f,
                    LaunchPosition: new Vector2(1f, 2f),
                    LaunchVisualHeightMeters: 1.75f,
                    OwnerEntityId: 7),
            });

        var plan = NetworkMatchSceneProjection.Build(snapshot, LocalId);

        var projectile = plan.Projectiles.Should().ContainSingle().Which;
        projectile.Position.Should().Be(new Vector3(5f, 2.25f, 7f));
        projectile.Velocity.Should().Be(new Vector3(3f, 6f, 4f));
        projectile.Id.Should().Be(1);
        projectile.LaunchPosition.Should().Be(new Vector3(1f, 1.75f, 2f));
        projectile.OwnerEntityId.Should().Be(7);
    }

    [Fact]
    public void A_snapshot_with_no_projectiles_yields_an_empty_projectile_list()
    {
        var plan = NetworkMatchSceneProjection.Build(Snapshot(Entity(LocalId, Vector2.Zero)), LocalId);

        plan.Projectiles.Should().BeEmpty();
    }

    private static void AssertOverview(CameraView3D camera)
    {
        AssertVector(
            camera.Position,
            new Vector3(0f, NetworkMatchSceneProjection.OverviewHeightMeters, -NetworkMatchSceneProjection.OverviewBackMeters));
        AssertVector(camera.Target, Vector3.Zero);
    }

    private static void AssertVector(Vector3 actual, Vector3 expected)
    {
        actual.X.Should().BeApproximately(expected.X, Tolerance);
        actual.Y.Should().BeApproximately(expected.Y, Tolerance);
        actual.Z.Should().BeApproximately(expected.Z, Tolerance);
    }

    [Fact]
    public void The_felled_prop_set_is_carried_from_the_snapshot_onto_the_plan()
    {
        var props = new[]
        {
            new PropSnapshot(PropId: 3, State: Garupan.Sim.Components.PropState.Toppling, FallYawRadians: 1f, ToppleSeconds: 0.2f),
        };
        var snapshot = new WorldSnapshot(Tick.Zero, new[] { Entity(LocalId, Vector2.Zero) }, Array.Empty<ProjectileSnapshot>())
        {
            Props = props,
        };

        var plan = NetworkMatchSceneProjection.Build(snapshot, LocalId);

        plan.DestroyedProps.Should().BeEquivalentTo(props);
    }

    private static WorldSnapshot Snapshot(params EntitySnapshot[] entities) =>
        new(Tick.Zero, entities, Array.Empty<ProjectileSnapshot>());

    private static EntitySnapshot Entity(
        uint id,
        Vector2 position,
        float yaw = 0f,
        EntityStateFlags flags = EntityStateFlags.None) =>
        new((int)id, position, yaw, 0f, flags);
}
