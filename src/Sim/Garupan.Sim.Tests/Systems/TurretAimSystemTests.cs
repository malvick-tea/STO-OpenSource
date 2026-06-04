using System;
using FluentAssertions;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Systems;
using Opus.Foundation;
using Xunit;

namespace Garupan.Sim.Tests.Systems;

public sealed class TurretAimSystemTests
{
    [Fact]
    public void Steps_toward_target_at_traverse_speed()
    {
        using var world = World.Create();
        var tank = world.Spawn(
            new Turret { YawRadians = 0f, TraverseSpeedRadPerS = MathF.PI / 4f }, // 45°/s
            new TurretTarget { YawRadians = MathF.PI }); // 180° away

        new TurretAimSystem().Tick(MakeCtx(world));

        var dt = 1f / 60f;
        var expectedStep = MathF.PI / 4f * dt;
        // Counter-clockwise step is positive yaw (delta = π wraps to π, sign positive).
        MathF.Abs(world.Get<Turret>(tank).YawRadians).Should().BeApproximately(expectedStep, 0.001f);
    }

    [Fact]
    public void Snaps_when_within_step()
    {
        using var world = World.Create();
        var tank = world.Spawn(
            new Turret { YawRadians = 0f, TraverseSpeedRadPerS = MathF.PI },
            new TurretTarget { YawRadians = 0.01f });

        new TurretAimSystem().Tick(MakeCtx(world));

        // Target within reach in one tick → snap
        world.Get<Turret>(tank).YawRadians.Should().BeApproximately(0.01f, 0.0001f);
    }

    [Fact]
    public void Picks_shortest_path_around_pi()
    {
        // Turret at +0.1 rad, target at -0.1 rad. Naive subtraction gives -0.2 (correct
        // shortest path); just verifies wrap doesn't break this case.
        using var world = World.Create();
        var tank = world.Spawn(
            new Turret { YawRadians = 0.1f, TraverseSpeedRadPerS = MathF.PI / 8f },
            new TurretTarget { YawRadians = -0.1f });

        new TurretAimSystem().Tick(MakeCtx(world));

        // Should rotate clockwise (delta = -0.2). Yaw decreases.
        world.Get<Turret>(tank).YawRadians.Should().BeLessThan(0.1f);
    }

    [Fact]
    public void Knocked_out_tank_turret_does_not_move()
    {
        using var world = World.Create();
        var tank = world.Spawn(
            new Turret { YawRadians = 0f, TraverseSpeedRadPerS = MathF.PI },
            new TurretTarget { YawRadians = MathF.PI });
        world.Add(tank, default(KnockedOut));

        new TurretAimSystem().Tick(MakeCtx(world));

        world.Get<Turret>(tank).YawRadians.Should().Be(0f);
    }

    private static TickContext MakeCtx(World world) =>
        new(world, GameTime.AtRate(60).Advance(), SimSeed.Zero, new CommandBuffer());
}
