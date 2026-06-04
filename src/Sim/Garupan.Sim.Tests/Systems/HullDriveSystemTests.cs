using System.Numerics;
using FluentAssertions;
using Garupan.Content;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Spawn;
using Garupan.Sim.Systems;
using Opus.Foundation;
using Xunit;

namespace Garupan.Sim.Tests.Systems;

public sealed class HullDriveSystemTests
{
    [Fact]
    public void Forward_throttle_accelerates_along_yaw()
    {
        using var world = World.Create();
        var tank = Spawn(world, new DriveInput { Throttle = 1f });

        new HullDriveSystem().Tick(MakeCtx(world));

        var hull = world.Get<Hull>(tank);
        world.Get<Transform>(tank).Position.X.Should().BeGreaterThan(0f);
        hull.DynamicsState.VelocityMps.X.Should().BeGreaterThan(0f);
        hull.DynamicsState.EngineRpm.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Negative_throttle_accelerates_in_reverse()
    {
        using var world = World.Create();
        var tank = Spawn(world, new DriveInput { Throttle = -1f });

        new HullDriveSystem().Tick(MakeCtx(world));

        world.Get<Transform>(tank).Position.X.Should().BeLessThan(0f);
    }

    [Fact]
    public void Steering_accumulates_yaw_inertia()
    {
        using var world = World.Create();
        var tank = Spawn(world, new DriveInput { Steering = 1f });

        new HullDriveSystem().Tick(MakeCtx(world));

        var hull = world.Get<Hull>(tank);
        world.Get<Transform>(tank).YawRadians.Should().BeGreaterThan(0f);
        hull.DynamicsState.AngularVelocityRadPerSec.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Terrain_sampler_moves_an_idle_tank_down_a_slope_too_steep_to_grip()
    {
        using var world = World.Create();
        var tank = Spawn(world, default);

        new HullDriveSystem((x, _) => x).Tick(MakeCtx(world));

        world.Get<Transform>(tank).Position.X.Should().BeLessThan(0f);
    }

    [Fact]
    public void Knocked_out_tank_does_not_drive()
    {
        using var world = World.Create();
        var tank = Spawn(world, new DriveInput { Throttle = 1f });
        world.Add(tank, default(KnockedOut));

        new HullDriveSystem().Tick(MakeCtx(world));

        world.Get<Transform>(tank).Position.Should().Be(Vector2.Zero);
    }

    [Fact]
    public void Out_of_range_throttle_is_clamped()
    {
        using var first = World.Create();
        using var second = World.Create();
        var clamped = Spawn(first, new DriveInput { Throttle = 1f });
        var excessive = Spawn(second, new DriveInput { Throttle = 5f });

        new HullDriveSystem().Tick(MakeCtx(first));
        new HullDriveSystem().Tick(MakeCtx(second));

        second.Get<Transform>(excessive).Position.Should().Be(first.Get<Transform>(clamped).Position);
    }

    private static EntityHandle Spawn(World world, DriveInput input) =>
        world.Spawn(
            new Transform(Vector2.Zero, 0f),
            new Hull
            {
                Dynamics = GroundVehiclePhysicsFactory.Build(TankRoster.VehicleMediumA.Mobility),
            },
            input);

    private static TickContext MakeCtx(World world) =>
        new(world, GameTime.AtRate(60).Advance(), SimSeed.Zero, new CommandBuffer());
}
