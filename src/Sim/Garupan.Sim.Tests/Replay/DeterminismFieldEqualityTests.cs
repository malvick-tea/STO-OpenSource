using System;
using System.Collections.Generic;
using System.Numerics;
using FluentAssertions;
using Garupan.Content;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Snapshot;
using Garupan.Sim.Spawn;
using Garupan.Sim.Systems;
using Opus.Foundation;
using Xunit;

namespace Garupan.Sim.Tests.Replay;

/// <summary>
/// Tick-by-tick field equality is the strongest determinism gate the project ships.
/// <see cref="DeterminismHarnessTests"/> verifies that two replay byte streams hash to
/// the same digest; this suite goes further — when the digests differ, it pinpoints the
/// first tick AND the first field that diverged, so a regression report identifies the
/// system to blame instead of "the hash changed somewhere in 60 ticks".
///
/// Two scenarios run side-by-side:
/// <list type="bullet">
/// <item><description>Single-tank, no AI. Bounds the deterministic property of the
///     input → hull → turret → projectile chain.</description></item>
/// <item><description>Multi-opponent (canonical demo composition: player + a heavy tank + 2×
///     a medium tank). Bounds the AI bot system + multi-entity ordering + projectile
///     fan-out under realistic load.</description></item>
/// </list>
/// </summary>
public sealed class DeterminismFieldEqualityTests
{
    private const int CanonicalTickCount = 120;

    [Fact]
    public void Single_tank_scenario_produces_identical_snapshots_tick_by_tick()
    {
        var first = RunSingleTankScenario(CanonicalTickCount);
        var second = RunSingleTankScenario(CanonicalTickCount);

        AssertSnapshotsAreIdentical(first, second);
    }

    [Fact]
    public void Multi_opponent_scenario_produces_identical_snapshots_tick_by_tick()
    {
        var first = RunMultiOpponentScenario(CanonicalTickCount);
        var second = RunMultiOpponentScenario(CanonicalTickCount);

        AssertSnapshotsAreIdentical(first, second);
    }

    [Fact]
    public void Determinism_holds_across_three_consecutive_runs()
    {
        var first = RunMultiOpponentScenario(CanonicalTickCount);
        var second = RunMultiOpponentScenario(CanonicalTickCount);
        var third = RunMultiOpponentScenario(CanonicalTickCount);

        AssertSnapshotsAreIdentical(first, second);
        AssertSnapshotsAreIdentical(second, third);
    }

    private static IReadOnlyList<WorldSnapshot> RunSingleTankScenario(int tickCount)
    {
        using var world = World.Create();

        TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumA,
            new Vector2(0f, 0f),
            yawRadians: 0f,
            Team.PlayerSchool,
            TankControl.Player);

        return RunPipeline(world, tickCount);
    }

    private static IReadOnlyList<WorldSnapshot> RunMultiOpponentScenario(int tickCount)
    {
        using var world = World.Create();

        TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumA,
            new Vector2(0f, 0f),
            yawRadians: 0f,
            Team.PlayerSchool,
            TankControl.Player);

        TankSpawner.Spawn(
            world,
            TankRoster.VehicleHeavyA,
            new Vector2(18f, 18f),
            yawRadians: MathF.PI,
            Team.OpponentSchool,
            TankControl.AiBot);

        TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumB,
            new Vector2(-15f, 22f),
            yawRadians: -3f * MathF.PI / 4f,
            Team.OpponentSchool,
            TankControl.AiBot);

        TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumB,
            new Vector2(20f, -8f),
            yawRadians: 3f * MathF.PI / 4f,
            Team.OpponentSchool,
            TankControl.AiBot);

        return RunPipeline(world, tickCount);
    }

    private static IReadOnlyList<WorldSnapshot> RunPipeline(World world, int tickCount)
    {
        var pipeline = new SystemPipeline(new ISystem[]
        {
            new ApplyInputsSystem(),
            new AiBotSystem(),
            new HullDriveSystem(),
            new TurretAimSystem(),
            new GunRecoilTickSystem(),
            new ReloadTickSystem(),
            new ProjectileIntegrateSystem(),
            new GunFireSystem(),
            new ProjectileHitResolveSystem(),
            new LifetimeDecaySystem(),
            new CleanupDeadSystem(),
        });

        var time = GameTime.AtRate(SimulationConstants.TicksPerSecond);
        var snapshots = new List<WorldSnapshot>(tickCount);

        for (var i = 0; i < tickCount; i++)
        {
            pipeline.Tick(world, time, SimSeed.Zero);
            time = time.Advance();
            snapshots.Add(SnapshotCapture.Capture(world, time.Tick));
        }

        return snapshots;
    }

    private static void AssertSnapshotsAreIdentical(
        IReadOnlyList<WorldSnapshot> expected,
        IReadOnlyList<WorldSnapshot> actual)
    {
        actual.Should().HaveCount(expected.Count, "tick count must match");

        for (var t = 0; t < expected.Count; t++)
        {
            var e = expected[t];
            var a = actual[t];

            a.Tick.Should().Be(e.Tick, $"tick number at index {t} must match");

            a.Entities.Should().HaveCount(e.Entities.Count, $"entity count at tick {e.Tick} must match");
            for (var i = 0; i < e.Entities.Count; i++)
            {
                AssertEntitySnapshotEqual(e.Entities[i], a.Entities[i], e.Tick, i);
            }

            a.Projectiles.Should().HaveCount(e.Projectiles.Count, $"projectile count at tick {e.Tick} must match");
            for (var i = 0; i < e.Projectiles.Count; i++)
            {
                AssertProjectileSnapshotEqual(e.Projectiles[i], a.Projectiles[i], e.Tick, i);
            }
        }
    }

    private static void AssertEntitySnapshotEqual(
        EntitySnapshot expected,
        EntitySnapshot actual,
        Tick tick,
        int index)
    {
        var ctx = $"entity[{index}] at tick {tick}";
        actual.Id.Should().Be(expected.Id, $"{ctx}: Id");
        actual.Position.X.Should().Be(expected.Position.X, $"{ctx}: Position.X");
        actual.Position.Y.Should().Be(expected.Position.Y, $"{ctx}: Position.Y");
        actual.YawRadians.Should().Be(expected.YawRadians, $"{ctx}: YawRadians");
        actual.TurretYawRadians.Should().Be(expected.TurretYawRadians, $"{ctx}: TurretYawRadians");
        actual.BarrelPitchRadians.Should().Be(expected.BarrelPitchRadians, $"{ctx}: BarrelPitchRadians");
        actual.MinBarrelPitchRadians.Should().Be(expected.MinBarrelPitchRadians, $"{ctx}: MinBarrelPitchRadians");
        actual.MaxBarrelPitchRadians.Should().Be(expected.MaxBarrelPitchRadians, $"{ctx}: MaxBarrelPitchRadians");
        actual.GunRecoilTravelMeters.Should().Be(expected.GunRecoilTravelMeters, $"{ctx}: GunRecoilTravelMeters");
        actual.StateFlags.Should().Be(expected.StateFlags, $"{ctx}: StateFlags");
    }

    private static void AssertProjectileSnapshotEqual(
        ProjectileSnapshot expected,
        ProjectileSnapshot actual,
        Tick tick,
        int index)
    {
        var ctx = $"projectile[{index}] at tick {tick}";
        actual.Id.Should().Be(expected.Id, $"{ctx}: Id");
        actual.Position.X.Should().Be(expected.Position.X, $"{ctx}: Position.X");
        actual.Position.Y.Should().Be(expected.Position.Y, $"{ctx}: Position.Y");
        actual.Velocity.X.Should().Be(expected.Velocity.X, $"{ctx}: Velocity.X");
        actual.Velocity.Y.Should().Be(expected.Velocity.Y, $"{ctx}: Velocity.Y");
        actual.Family.Should().Be(expected.Family, $"{ctx}: Family");
        actual.VisualHeightMeters.Should().Be(expected.VisualHeightMeters, $"{ctx}: VisualHeightMeters");
        actual.VerticalVelocityMps.Should().Be(expected.VerticalVelocityMps, $"{ctx}: VerticalVelocityMps");
        actual.DistanceTravelledMeters.Should().Be(expected.DistanceTravelledMeters, $"{ctx}: DistanceTravelledMeters");
        actual.OwnerEntityId.Should().Be(expected.OwnerEntityId, $"{ctx}: OwnerEntityId");
    }
}
