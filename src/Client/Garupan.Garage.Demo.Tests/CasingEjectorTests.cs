using System;
using System.Numerics;
using FluentAssertions;
using Garupan.Sim.Components;
using Garupan.Sim.Snapshot;
using Xunit;

namespace Garupan.Garage.Demo.Tests;

/// <summary>Pure-logic verification of <see cref="CasingEjector"/>. No GPU, no Sim
/// world — every test feeds primitive <see cref="ProjectileSnapshot"/> +
/// <see cref="TankPose"/> inputs and checks <see cref="CasingEjector.LiveCount"/> /
/// <see cref="CasingEjector.CasingMatrices"/>.</summary>
public sealed class CasingEjectorTests
{
    private const float Epsilon = 1e-3f;
    private const float SmallDeltaSeconds = 0.016f;

    [Fact]
    public void Update_spawns_a_casing_for_each_new_projectile_within_assignment_radius()
    {
        var ejector = new CasingEjector();
        var projectile = ProjectileAt(id: 1, position: new Vector2(10f, 0f));
        var tank = AliveTankAt(position: new Vector2(10f, 0f), simYaw: 0f);

        ejector.Update(new[] { projectile }, new[] { tank }, SmallDeltaSeconds);

        ejector.LiveCount.Should().Be(1);
        ejector.CasingMatrices.Should().HaveCount(1);
    }

    [Fact]
    public void Update_does_not_double_spawn_when_projectile_persists_across_ticks()
    {
        var ejector = new CasingEjector();
        var projectile = ProjectileAt(id: 1, position: new Vector2(10f, 0f));
        var tank = AliveTankAt(position: new Vector2(10f, 0f), simYaw: 0f);

        ejector.Update(new[] { projectile }, new[] { tank }, SmallDeltaSeconds);
        ejector.Update(new[] { projectile }, new[] { tank }, SmallDeltaSeconds);
        ejector.Update(new[] { projectile }, new[] { tank }, SmallDeltaSeconds);

        ejector.LiveCount.Should().Be(1);
    }

    [Fact]
    public void Update_spawns_one_casing_per_new_projectile_id_in_a_batch()
    {
        var ejector = new CasingEjector();
        var tank = AliveTankAt(position: new Vector2(10f, 0f), simYaw: 0f);
        var batch = new[]
        {
            ProjectileAt(id: 1, position: new Vector2(10f, 0f)),
            ProjectileAt(id: 2, position: new Vector2(10f, 0f)),
            ProjectileAt(id: 3, position: new Vector2(10f, 0f)),
        };

        ejector.Update(batch, new[] { tank }, SmallDeltaSeconds);

        ejector.LiveCount.Should().Be(3);
    }

    [Fact]
    public void Update_skips_spawn_when_no_alive_tank_within_assignment_radius()
    {
        var ejector = new CasingEjector();
        var projectile = ProjectileAt(id: 1, position: Vector2.Zero);
        var farTank = AliveTankAt(position: new Vector2(100f, 100f), simYaw: 0f);

        ejector.Update(new[] { projectile }, new[] { farTank }, SmallDeltaSeconds);

        ejector.LiveCount.Should().Be(0);
    }

    [Fact]
    public void Update_skips_knocked_out_tank_when_assigning_shooter()
    {
        var ejector = new CasingEjector();
        var projectile = ProjectileAt(id: 1, position: new Vector2(10f, 0f));
        var koTank = new TankPose(PositionXY: new Vector2(10f, 0f), SimYawRadians: 0f, IsAlive: false);

        ejector.Update(new[] { projectile }, new[] { koTank }, SmallDeltaSeconds);

        ejector.LiveCount.Should().Be(0);
    }

    [Fact]
    public void Update_picks_the_closer_alive_tank_when_two_are_in_radius()
    {
        // Both within 5m of projectile at sim (5,0). nearTank is closer.
        var ejector = new CasingEjector();
        var projectile = ProjectileAt(id: 1, position: new Vector2(5f, 0f));
        var farTank = AliveTankAt(position: new Vector2(8f, 0f), simYaw: 0f);
        var nearTank = AliveTankAt(position: new Vector2(6f, 0f), simYaw: MathF.PI);

        ejector.Update(new[] { projectile }, new[] { farTank, nearTank }, SmallDeltaSeconds);

        ejector.LiveCount.Should().Be(1);
        // nearTank: simYaw=π → worldYaw=-π → forward=(cos(-π),0,-sin(-π))=(-1,0,0) → rear=(+1,0,0).
        // Spawn = nearTank world (6,0,0) + rear*RearOffset + UnitY*EjectionHeight.
        var translation = ejector.CasingMatrices[0].Translation;
        translation.X.Should().BeApproximately(6f + CasingEjectorConfig.Default.EjectionRearOffsetMeters, Epsilon);
        translation.Y.Should().BeApproximately(CasingEjectorConfig.Default.EjectionHeightMeters, Epsilon);
        translation.Z.Should().BeApproximately(0f, Epsilon);
    }

    [Fact]
    public void Update_expires_a_casing_after_its_lifetime_elapses()
    {
        var config = CasingEjectorConfig.Default with { LifetimeSeconds = 1.0f };
        var ejector = new CasingEjector(config);
        var projectile = ProjectileAt(id: 1, position: Vector2.Zero);
        var tank = AliveTankAt(position: Vector2.Zero, simYaw: 0f);

        ejector.Update(new[] { projectile }, new[] { tank }, SmallDeltaSeconds);
        ejector.LiveCount.Should().Be(1);

        for (var i = 0; i < 4; i++)
        {
            ejector.Update(Array.Empty<ProjectileSnapshot>(), Array.Empty<TankPose>(), 0.3f);
        }

        ejector.LiveCount.Should().Be(0);
    }

    [Fact]
    public void Update_integrates_gravity_into_the_casing_y_position()
    {
        // Strip horizontal ejection so the casing starts at rest with no rear offset; gravity
        // is the only force acting on Y over the integration window.
        var config = CasingEjectorConfig.Default with
        {
            EjectionRearOffsetMeters = 0f,
            EjectionRearSpeedMps = 0f,
            EjectionUpwardSpeedMps = 0f,
            LifetimeSeconds = 10f,
        };
        var ejector = new CasingEjector(config);
        var projectile = ProjectileAt(id: 1, position: Vector2.Zero);
        var tank = AliveTankAt(position: Vector2.Zero, simYaw: 0f);

        ejector.Update(new[] { projectile }, new[] { tank }, deltaSeconds: 0f);
        var initialY = ejector.CasingMatrices[0].Translation.Y;
        ejector.Update(Array.Empty<ProjectileSnapshot>(), Array.Empty<TankPose>(), deltaSeconds: 1f);
        var afterY = ejector.CasingMatrices[0].Translation.Y;

        // After 1s with zero initial vertical velocity: vel = -9.81, pos += -9.81*1.
        afterY.Should().BeApproximately(initialY + (config.GravityMps2.Y * 1f), Epsilon * 10f);
    }

    [Fact]
    public void Update_ignores_non_finite_or_negative_delta()
    {
        var ejector = new CasingEjector();
        var projectile = ProjectileAt(id: 1, position: Vector2.Zero);
        var tank = AliveTankAt(position: Vector2.Zero, simYaw: 0f);

        ejector.Update(new[] { projectile }, new[] { tank }, float.NaN);
        ejector.Update(new[] { projectile }, new[] { tank }, float.PositiveInfinity);
        ejector.Update(new[] { projectile }, new[] { tank }, float.NegativeInfinity);
        ejector.Update(new[] { projectile }, new[] { tank }, -0.1f);

        ejector.LiveCount.Should().Be(0);
    }

    [Fact]
    public void Reset_clears_live_casings_and_tracked_ids()
    {
        var ejector = new CasingEjector();
        var projectile = ProjectileAt(id: 1, position: Vector2.Zero);
        var tank = AliveTankAt(position: Vector2.Zero, simYaw: 0f);

        ejector.Update(new[] { projectile }, new[] { tank }, SmallDeltaSeconds);
        ejector.LiveCount.Should().Be(1);

        ejector.Reset();

        ejector.LiveCount.Should().Be(0);
        ejector.CasingMatrices.Should().BeEmpty();

        // After Reset, the same projectile id should be treated as new again.
        ejector.Update(new[] { projectile }, new[] { tank }, SmallDeltaSeconds);
        ejector.LiveCount.Should().Be(1);
    }

    private static ProjectileSnapshot ProjectileAt(int id, Vector2 position) =>
        new(Id: id, Position: position, Velocity: Vector2.Zero, Family: AmmoType.AP);

    private static TankPose AliveTankAt(Vector2 position, float simYaw) =>
        new(PositionXY: position, SimYawRadians: simYaw, IsAlive: true);
}
