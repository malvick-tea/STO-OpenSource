using System.Numerics;
using FluentAssertions;
using Garupan.Content;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Spawn;
using Garupan.Sim.Systems;
using Opus.Engine.Physics.Destruction;
using Opus.Foundation;
using Xunit;

namespace Garupan.Sim.Tests.Systems;

public sealed class PropDestructionTests
{
    [Fact]
    public void A_charging_tank_topples_a_tree_toward_its_travel()
    {
        using var world = World.Create();
        SpawnTank(world, Vector2.Zero, new Vector2(5f, 0f), throttle: 1f);
        var tree = SpawnProp(world, PropKind.Tree, new Vector2(3f, 0f), diameter: 0.3f, height: 10f);

        Tick(world);

        var prop = world.Get<DestructibleProp>(tree);
        prop.State.Should().Be(PropState.Toppling);
        prop.FallYawRadians.Should().BeApproximately(0f, 1e-3f); // felled toward +X, the way the tank charged
    }

    [Fact]
    public void A_charging_tank_spends_kinetic_energy_to_topple_a_tree()
    {
        using var world = World.Create();
        var tank = SpawnTank(world, Vector2.Zero, new Vector2(5f, 0f), throttle: 1f);
        var tree = SpawnProp(world, PropKind.Tree, new Vector2(3f, 0f), diameter: 0.3f, height: 10f);

        var beforeHull = world.Get<Hull>(tank);
        var massKg = beforeHull.Dynamics!.MassKg;
        var beforeEnergy = PropResistance.KineticEnergyJoules(massKg, beforeHull.DynamicsState.VelocityMps.Length());

        Tick(world);

        var afterHull = world.Get<Hull>(tank);
        var afterEnergy = PropResistance.KineticEnergyJoules(massKg, afterHull.DynamicsState.VelocityMps.Length());
        var prop = world.Get<DestructibleProp>(tree);

        afterHull.DynamicsState.VelocityMps.X.Should().BeLessThan(5f);
        (beforeEnergy - afterEnergy).Should().BeApproximately(prop.ToppleEnergyJoules, 0.05f);
    }

    [Fact]
    public void A_charging_tank_breaks_a_light_sign()
    {
        using var world = World.Create();
        SpawnTank(world, Vector2.Zero, new Vector2(5f, 0f), throttle: 1f);
        var sign = SpawnProp(world, PropKind.TrafficSign, new Vector2(3f, 0f), diameter: 0.08f, height: 2.5f);

        Tick(world);

        world.Get<DestructibleProp>(sign).State.Should().Be(PropState.Broken);
    }

    [Fact]
    public void A_massive_trunk_blocks_a_parked_tank_and_pushes_it_clear()
    {
        using var world = World.Create();
        var tank = SpawnTank(world, Vector2.Zero, Vector2.Zero, throttle: 0f);
        var giant = SpawnProp(world, PropKind.Tree, new Vector2(3f, 0f), diameter: 1.0f, height: 14f);

        Tick(world);

        var prop = world.Get<DestructibleProp>(giant);
        prop.State.Should().Be(PropState.Standing);

        var separation = (new Vector2(3f, 0f) - world.Get<Transform>(tank).Position).Length();
        var contactDistance = world.Get<HitRadius>(tank).Meters + prop.RadiusMeters;
        separation.Should().BeGreaterThanOrEqualTo(contactDistance - 1e-3f);
    }

    [Fact]
    public void A_flooring_tank_pushes_a_thin_tree_over_from_rest()
    {
        using var world = World.Create();
        SpawnTank(world, Vector2.Zero, Vector2.Zero, throttle: 1f); // engine leaning on it, not yet rolling
        var thin = SpawnProp(world, PropKind.Tree, new Vector2(3f, 0f), diameter: 0.2f, height: 8f);

        Tick(world);

        world.Get<DestructibleProp>(thin).State.Should().Be(PropState.Toppling);
    }

    [Fact]
    public void A_parked_tank_off_throttle_does_not_fell_the_same_thin_tree()
    {
        using var world = World.Create();
        SpawnTank(world, Vector2.Zero, Vector2.Zero, throttle: 0f);
        var thin = SpawnProp(world, PropKind.Tree, new Vector2(3f, 0f), diameter: 0.2f, height: 8f);

        Tick(world);

        world.Get<DestructibleProp>(thin).State.Should().Be(PropState.Standing);
    }

    [Fact]
    public void A_felled_tree_settles_from_toppling_to_fallen()
    {
        using var world = World.Create();
        SpawnTank(world, Vector2.Zero, new Vector2(5f, 0f), throttle: 1f);
        var tree = SpawnProp(world, PropKind.Tree, new Vector2(3f, 0f), diameter: 0.3f, height: 10f);

        Tick(world);
        world.Get<DestructibleProp>(tree).State.Should().Be(PropState.Toppling);

        // Age past the topple duration (0.8 s at 60 Hz ≈ 48 ticks).
        for (var i = 0; i < 55; i++)
        {
            Tick(world);
        }

        world.Get<DestructibleProp>(tree).State.Should().Be(PropState.Fallen);
    }

    [Fact]
    public void Topple_energy_grows_with_the_cube_of_trunk_diameter()
    {
        using var world = World.Create();
        var thin = SpawnProp(world, PropKind.Tree, Vector2.Zero, diameter: 0.3f, height: 10f);
        var thick = SpawnProp(world, PropKind.Tree, new Vector2(80f, 0f), diameter: 0.6f, height: 10f);

        var thinProp = world.Get<DestructibleProp>(thin);
        var thickProp = world.Get<DestructibleProp>(thick);

        thinProp.Behavior.Should().Be(PropBehavior.Topple);
        (thickProp.ToppleEnergyJoules / thinProp.ToppleEnergyJoules).Should().BeApproximately(8f, 1e-2f);
        thickProp.ResistingForceNewtons.Should().BeGreaterThan(thinProp.ResistingForceNewtons);
    }

    private static EntityHandle SpawnTank(World world, Vector2 position, Vector2 velocity, float throttle)
    {
        var tank = TankSpawner.Spawn(world, TankRoster.VehicleMediumA, position, 0f, Team.PlayerSchool, TankControl.Player);
        ref var hull = ref world.Get<Hull>(tank);
        hull.DynamicsState = hull.DynamicsState with { VelocityMps = velocity };
        world.Get<DriveInput>(tank).Throttle = throttle;
        return tank;
    }

    private static EntityHandle SpawnProp(World world, PropKind kind, Vector2 position, float diameter, float height) =>
        MapPropSpawner.SpawnOne(world, new MapProp(kind, position, 0f, diameter, height), propId: 0);

    private static void Tick(World world) =>
        new PropCollisionSystem().Tick(new TickContext(world, GameTime.AtRate(60).Advance(), SimSeed.Zero, new CommandBuffer()));
}
