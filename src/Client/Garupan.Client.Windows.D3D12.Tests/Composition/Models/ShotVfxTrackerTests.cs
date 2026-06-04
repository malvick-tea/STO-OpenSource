using System;
using System.Numerics;
using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Garupan.Client.Windows.Direct3D12.Composition.Models;
using Opus.Engine.Ui;
using Xunit;

namespace Garupan.Client.Windows.Direct3D12.Tests.Composition.Models;

public sealed class ShotVfxTrackerTests
{
    private static readonly CameraView3D Camera =
        CameraView3D.LookAt(new Vector3(0f, 5f, -10f), Vector3.Zero);

    [Fact]
    public void New_projectile_creates_one_burst_at_its_launch_origin_not_its_current_position()
    {
        var tracker = new ShotVfxTracker();
        var launch = new Vector3(2f, 1.5f, 3f);
        var plan = Plan(10, new ProjectilePlacement(new Vector3(50f, 2f, 3f), Vector3.UnitX, 7, launch));

        var burst = tracker.Resolve(plan).Should().ContainSingle().Which;

        burst.MuzzlePosition.Should().Be(launch);
        burst.DustPosition.Should().Be(new Vector3(2f, 0f, 3f));
        burst.InitialGasVelocity.Should().Be(new Vector3(11f, 0f, 0f));
    }

    [Fact]
    public void Repeated_plan_and_later_snapshots_do_not_duplicate_an_active_projectile_burst()
    {
        var tracker = new ShotVfxTracker();
        var projectile = new ProjectilePlacement(Vector3.One, Vector3.UnitX, 7, new Vector3(2f, 1.5f, 3f));

        tracker.Resolve(Plan(10, projectile)).Should().ContainSingle();
        tracker.Resolve(Plan(10, projectile)).Should().ContainSingle();
        tracker.Resolve(Plan(11, projectile)).Should().ContainSingle();
    }

    [Fact]
    public void Burst_expires_after_visual_lifetime()
    {
        var tracker = new ShotVfxTracker();
        tracker.Resolve(Plan(0, new ProjectilePlacement(Vector3.One, Vector3.UnitX, 7, Vector3.One)))
            .Should().ContainSingle();

        // 150 ticks at 60 Hz = 2.5 s, past the 2.2 s cordite-cloud lifetime.
        tracker.Resolve(Plan(150)).Should().BeEmpty();
    }

    [Fact]
    public void Gas_first_moves_along_barrel_then_buoyancy_dominates()
    {
        var burst = new ShotVfxBurst(Vector3.Zero, Vector3.Zero, new Vector3(11f, 0f, 0f), 0f);

        var early = ShotVfxTracker.GasPosition(burst, 0.05f);
        var almostLate = ShotVfxTracker.GasPosition(burst, 0.85f);
        var late = ShotVfxTracker.GasPosition(burst, 0.90f);
        var lateStep = late - almostLate;

        early.X.Should().BeGreaterThan(early.Y);
        late.Y.Should().BeGreaterThan(early.Y);
        lateStep.Y.Should().BeGreaterThan(lateStep.X);
        lateStep.X.Should().BeLessThan(early.X);
    }

    [Fact]
    public void Flash_plume_starts_laid_out_along_the_barrel_axis()
    {
        var burst = new ShotVfxBurst(Vector3.Zero, Vector3.Zero, new Vector3(11f, 0f, 0f), 0f);

        var inner = ShotVfxTracker.FlashPlumePosition(burst, 0f, 0.1f);
        var outer = ShotVfxTracker.FlashPlumePosition(burst, 0f, 0.9f);
        var moving = ShotVfxTracker.FlashPlumePosition(burst, 0.05f, 0.9f);

        inner.Should().Be(new Vector3(0.1f, 0f, 0f));
        outer.Should().Be(new Vector3(0.9f, 0f, 0f));
        moving.X.Should().BeGreaterThan(outer.X);
        moving.Y.Should().Be(0f);
    }

    private static NetworkMatchScenePlan Plan(long tick, params ProjectilePlacement[] projectiles) =>
        new(Camera, Array.Empty<TankPlacement>())
        {
            SnapshotTick = tick,
            Projectiles = projectiles,
        };
}
