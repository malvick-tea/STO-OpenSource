using System.Numerics;
using FluentAssertions;
using Garupan.Content;
using Garupan.Sim;
using Garupan.Sim.Collision;
using Garupan.Sim.Components;
using Garupan.Sim.Spawn;
using Garupan.Sim.Systems;
using Opus.Foundation;
using Xunit;

namespace Garupan.Sim.Tests.Systems;

public sealed class ObstacleCollisionSystemTests
{
    [Fact]
    public void A_tank_driving_into_a_building_is_stopped_flush_against_the_wall()
    {
        using var world = World.Create();
        var tank = SpawnTank(world, new Vector2(6.5f, 0f), velocity: new Vector2(5f, 0f));
        var radius = world.Get<HitRadius>(tank).Meters;
        SpawnObstacle(world, new Vector2(10f, 0f), yaw: 0f, halfWidth: 3f, halfDepth: 8f);

        Tick(world);

        var position = world.Get<Transform>(tank).Position;
        position.X.Should().BeLessThan(6.5f, "the building pushes the hull back out of the overlap");
        // Near face is at x = 7; the hull centre settles a radius clear of it and no longer overlaps.
        position.X.Should().BeApproximately(7f - radius, 1e-3f);
        world.Get<Hull>(tank).DynamicsState.VelocityMps.X.Should().BeApproximately(0f, 1e-3f);
    }

    [Fact]
    public void A_tank_clear_of_every_building_is_untouched()
    {
        using var world = World.Create();
        var tank = SpawnTank(world, new Vector2(0f, 0f), velocity: new Vector2(4f, 0f));
        SpawnObstacle(world, new Vector2(60f, 0f), yaw: 0f, halfWidth: 5f, halfDepth: 5f);

        Tick(world);

        world.Get<Transform>(tank).Position.Should().Be(Vector2.Zero);
        world.Get<Hull>(tank).DynamicsState.VelocityMps.Should().Be(new Vector2(4f, 0f));
    }

    [Fact]
    public void A_tank_grazing_a_wall_keeps_its_along_wall_speed_and_loses_only_the_inward_part()
    {
        using var world = World.Create();
        // A long wall lying along X with its near face at z = 9; the tank skims it while driving
        // east and leaning slightly into it.
        var tank = SpawnTank(world, new Vector2(0f, 8.5f), velocity: new Vector2(5f, 2f));
        SpawnObstacle(world, new Vector2(0f, 10f), yaw: 0f, halfWidth: 20f, halfDepth: 1f);

        Tick(world);

        var velocity = world.Get<Hull>(tank).DynamicsState.VelocityMps;
        velocity.X.Should().BeApproximately(5f, 1e-3f, "the along-wall component survives — the tank slides");
        velocity.Y.Should().BeApproximately(0f, 1e-3f, "only the into-wall component is cancelled");
    }

    [Fact]
    public void A_rotated_building_blocks_along_its_own_oriented_face()
    {
        using var world = World.Create();
        var tank = SpawnTank(world, new Vector2(0f, 0f), velocity: new Vector2(3f, 3f));
        var radius = world.Get<HitRadius>(tank).Meters;
        // A box turned 45°: its corner points back at the origin, so the contact normal runs along
        // the diagonal and the hull is pushed straight back down it.
        SpawnObstacle(world, new Vector2(4f, 4f), yaw: MathF.PI / 4f, halfWidth: 3f, halfDepth: 3f);

        Tick(world);

        var contact = ResolveContact(world, tank);
        contact.Overlaps.Should().BeFalse("the hull ends clear of the rotated footprint");
        var velocity = world.Get<Hull>(tank).DynamicsState.VelocityMps;
        Vector2.Dot(velocity, Vector2.Normalize(new Vector2(1f, 1f)))
            .Should().BeApproximately(0f, 1e-3f, "the diagonal (inward) component is cancelled");
        radius.Should().BeGreaterThan(0f);
    }

    private static EntityHandle SpawnTank(World world, Vector2 position, Vector2 velocity)
    {
        var tank = TankSpawner.Spawn(world, TankRoster.VehicleMediumA, position, 0f, Team.PlayerSchool, TankControl.Player);
        ref var hull = ref world.Get<Hull>(tank);
        hull.DynamicsState = hull.DynamicsState with { VelocityMps = velocity };
        return tank;
    }

    private static EntityHandle SpawnObstacle(World world, Vector2 center, float yaw, float halfWidth, float halfDepth) =>
        MapObstacleSpawner.SpawnOne(world, new MapObstacle(center, yaw, halfWidth, halfDepth, HeightMeters: 18f));

    private static CircleBoxContact ResolveContact(World world, EntityHandle tank)
    {
        var position = world.Get<Transform>(tank).Position;
        var radius = world.Get<HitRadius>(tank).Meters;
        // Re-derive the single obstacle's frame the same way the spawner did, to assert separation.
        var obstacle = default(StaticObstacle);
        var query = new Arch.Core.QueryDescription().WithAll<StaticObstacle, Transform>();
        var center = Vector2.Zero;
        world.Raw.Query(in query, (ref Transform tf, ref StaticObstacle obs) =>
        {
            obstacle = obs;
            center = tf.Position;
        });
        return CircleBoxCollision.Resolve(position, radius, center, obstacle.LocalRight, obstacle.LocalForward, obstacle.HalfExtents);
    }

    private static void Tick(World world) =>
        new ObstacleCollisionSystem().Tick(new TickContext(world, GameTime.AtRate(60).Advance(), SimSeed.Zero, new CommandBuffer()));
}
