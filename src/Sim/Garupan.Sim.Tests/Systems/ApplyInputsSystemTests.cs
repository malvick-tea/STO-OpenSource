using FluentAssertions;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Systems;
using Opus.Foundation;
using Xunit;

namespace Garupan.Sim.Tests.Systems;

public sealed class ApplyInputsSystemTests
{
    [Fact]
    public void Pending_throttle_and_steering_commit_to_drive_input()
    {
        using var world = World.Create();
        var tank = world.Spawn(
            default(DriveInput),
            default(TurretTarget),
            new PendingInput { Throttle = 0.5f, Steering = -0.25f });

        new ApplyInputsSystem().Tick(MakeCtx(world));

        var drive = world.Get<DriveInput>(tank);
        drive.Throttle.Should().Be(0.5f);
        drive.Steering.Should().Be(-0.25f);
    }

    [Fact]
    public void Out_of_range_throttle_and_steering_are_clamped()
    {
        using var world = World.Create();
        var tank = world.Spawn(
            default(DriveInput),
            default(TurretTarget),
            new PendingInput { Throttle = 5f, Steering = -3f });

        new ApplyInputsSystem().Tick(MakeCtx(world));

        var drive = world.Get<DriveInput>(tank);
        drive.Throttle.Should().Be(1f);
        drive.Steering.Should().Be(-1f);
    }

    [Fact]
    public void Pending_turret_yaw_commits_to_turret_target()
    {
        using var world = World.Create();
        var tank = world.Spawn(
            default(DriveInput),
            default(TurretTarget),
            new PendingInput { TurretYawRadians = 1.5f });

        new ApplyInputsSystem().Tick(MakeCtx(world));

        world.Get<TurretTarget>(tank).YawRadians.Should().Be(1.5f);
    }

    [Fact]
    public void Fire_flag_attaches_fire_intent_one_shot()
    {
        using var world = World.Create();
        var tank = world.Spawn(
            default(DriveInput),
            default(TurretTarget),
            new PendingInput { Flags = InputFlags.Fire });

        new ApplyInputsSystem().Tick(MakeCtx(world));

        world.Has<FireIntent>(tank).Should().BeTrue();
    }

    [Fact]
    public void Pending_barrel_pitch_is_clamped_and_committed_to_the_turret()
    {
        using var world = World.Create();
        var tank = world.Spawn(
            default(Turret),
            new GunMount { MinPitchRadians = -0.1f, MaxPitchRadians = 0.25f },
            new PendingInput { BarrelPitchRadians = 99f });

        new ApplyInputsSystem().Tick(MakeCtx(world));

        world.Get<Turret>(tank).BarrelPitchRadians.Should().Be(0.25f);
    }

    [Fact]
    public void No_fire_flag_does_not_attach_fire_intent()
    {
        using var world = World.Create();
        var tank = world.Spawn(
            default(DriveInput),
            default(TurretTarget),
            new PendingInput { Flags = InputFlags.None });

        new ApplyInputsSystem().Tick(MakeCtx(world));

        world.Has<FireIntent>(tank).Should().BeFalse();
    }

    [Fact]
    public void Pending_input_is_removed_after_apply()
    {
        using var world = World.Create();
        var tank = world.Spawn(
            default(DriveInput),
            default(TurretTarget),
            new PendingInput { Throttle = 0.5f });

        new ApplyInputsSystem().Tick(MakeCtx(world));

        world.Has<PendingInput>(tank).Should().BeFalse();
    }

    [Fact]
    public void Knocked_out_tank_keeps_pending_input_and_does_not_commit()
    {
        using var world = World.Create();
        var tank = world.Spawn(
            new DriveInput { Throttle = 0f },
            default(TurretTarget),
            new PendingInput { Throttle = 1f, Flags = InputFlags.Fire });
        world.Add(tank, default(KnockedOut));

        new ApplyInputsSystem().Tick(MakeCtx(world));

        // KnockedOut filtered: drive stays untouched, fire never attached, pending stays
        // (next frame's PendingInput attachment would overwrite it anyway).
        world.Get<DriveInput>(tank).Throttle.Should().Be(0f);
        world.Has<FireIntent>(tank).Should().BeFalse();
        world.Has<PendingInput>(tank).Should().BeTrue();
    }

    [Fact]
    public void Fire_flag_does_not_attach_a_second_intent_when_one_already_present()
    {
        using var world = World.Create();
        var tank = world.Spawn(
            default(DriveInput),
            default(TurretTarget),
            new PendingInput { Flags = InputFlags.Fire });
        world.Add(tank, default(FireIntent));

        new ApplyInputsSystem().Tick(MakeCtx(world));

        // FireIntent is a marker; "already present" is the only invariant — no count to assert.
        world.Has<FireIntent>(tank).Should().BeTrue();
    }

    private static TickContext MakeCtx(World world) =>
        new(world, GameTime.AtRate(60).Advance(), SimSeed.Zero, new CommandBuffer());
}
