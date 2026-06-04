using System;
using System.Collections.Generic;
using System.Numerics;
using FluentAssertions;
using Garupan.Sim.Components;
using Garupan.Sim.Snapshot;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Xunit;
using ContentShellVisualCsv = Garupan.Content.ShellVisualCsv;
using ContentShellVisualGeometry = Garupan.Content.ShellVisualGeometry;

namespace Garupan.Garage.Demo.Tests;

/// <summary>Pure-logic verification of <see cref="SimToWorldMapper"/>. No GPU, no Sim
/// world — every test takes primitive inputs and checks the matrices that come out.</summary>
public sealed class SimToWorldMapperTests
{
    private const float Epsilon = 1e-5f;

    [Fact]
    public void SimXyToWorldXz_maps_origin_to_origin()
    {
        SimToWorldMapper.SimXyToWorldXz(Vector2.Zero).Should().Be(Vector3.Zero);
    }

    [Fact]
    public void SimXyToWorldXz_preserves_x_and_negates_y_into_z()
    {
        // Sim is top-down XY (Y is north); world is right-handed Y-up so Sim +Y → world -Z.
        var result = SimToWorldMapper.SimXyToWorldXz(new Vector2(3f, 7f));
        result.X.Should().BeApproximately(3f, Epsilon);
        result.Y.Should().Be(0f);
        result.Z.Should().BeApproximately(-7f, Epsilon);
    }

    [Fact]
    public void SimXyToWorldXz_keeps_y_at_zero_for_negative_xy_too()
    {
        var result = SimToWorldMapper.SimXyToWorldXz(new Vector2(-4f, -6f));
        result.Should().Be(new Vector3(-4f, 0f, 6f));
    }

    [Fact]
    public void KnockedOutProgress_returns_zero_when_ko_tick_is_null()
    {
        SimToWorldMapper.KnockedOutProgress(null, currentTick: 999L).Should().Be(0f);
    }

    [Fact]
    public void KnockedOutProgress_returns_zero_at_the_tick_of_ko()
    {
        // Animation hasn't started until the *next* tick after the KO is observed.
        SimToWorldMapper.KnockedOutProgress(koTick: 100L, currentTick: 100L).Should().Be(0f);
    }

    [Fact]
    public void KnockedOutProgress_clamps_to_zero_when_current_tick_is_earlier_than_ko_tick()
    {
        // Defensive: a host that captures koTick before currentTick advances shouldn't
        // surface negative progress.
        SimToWorldMapper.KnockedOutProgress(koTick: 100L, currentTick: 50L).Should().Be(0f);
    }

    [Fact]
    public void KnockedOutProgress_returns_half_at_half_of_animation_duration()
    {
        // Half of 0.5 s at 60 Hz = 15 ticks since KO.
        const long koTick = 100L;
        const long halfwayTick = 100L + 15L;
        SimToWorldMapper.KnockedOutProgress(koTick, halfwayTick).Should().BeApproximately(0.5f, Epsilon);
    }

    [Fact]
    public void KnockedOutProgress_reaches_one_at_exact_animation_duration()
    {
        // 30 ticks × (1/60 s) = 0.5 s = animation duration.
        const long koTick = 100L;
        const long endTick = 100L + 30L;
        SimToWorldMapper.KnockedOutProgress(koTick, endTick).Should().BeApproximately(1f, Epsilon);
    }

    [Fact]
    public void KnockedOutProgress_clamps_to_one_well_past_animation_duration()
    {
        SimToWorldMapper.KnockedOutProgress(koTick: 100L, currentTick: 9999L).Should().Be(1f);
    }

    [Fact]
    public void BuildTankWorld_matches_GarageSceneController_when_alive()
    {
        var simPos = new Vector2(10f, 5f);
        const float simYaw = MathF.PI / 4f;
        var actual = SimToWorldMapper.BuildTankWorld(simPos, simYaw, koTick: null, currentTick: 0L);
        var expected = GarageSceneController.BuildTankWorld(new Vector3(10f, 0f, -5f), -simYaw);
        actual.Should().Be(expected);
    }

    [Fact]
    public void BuildTankWorld_premultiplies_pitch_when_knocked_out_at_full_progress()
    {
        var simPos = new Vector2(0f, 0f);
        const float simYaw = 0f;
        var alive = SimToWorldMapper.BuildTankWorld(simPos, simYaw, koTick: null, currentTick: 0L);
        var fullyKo = SimToWorldMapper.BuildTankWorld(simPos, simYaw, koTick: 0L, currentTick: 30L);

        var expected = Matrix4x4.CreateRotationX(SimToWorldMapper.KnockedOutPitchRadians) * alive;
        fullyKo.Should().Be(expected);
    }

    [Fact]
    public void BuildTankWorld_scales_pitch_linearly_with_progress()
    {
        const long ko = 0L;
        const long halfwayTick = 15L;
        var simPos = new Vector2(2f, -3f);
        const float simYaw = MathF.PI / 6f;

        var halfway = SimToWorldMapper.BuildTankWorld(simPos, simYaw, ko, halfwayTick);
        var alive = SimToWorldMapper.BuildTankWorld(simPos, simYaw, koTick: null, currentTick: halfwayTick);
        var expected = Matrix4x4.CreateRotationX(SimToWorldMapper.KnockedOutPitchRadians * 0.5f) * alive;

        halfway.Should().Be(expected);
    }

    [Fact]
    public void BuildProjectileTrail_returns_empty_for_no_projectiles()
    {
        var empty = (IReadOnlyList<ProjectileSnapshot>)Array.Empty<ProjectileSnapshot>();
        SimToWorldMapper.BuildProjectileTrail(empty).Should().BeEmpty();
    }

    [Fact]
    public void BuildProjectileTrail_emits_four_matrices_per_projectile()
    {
        var projectiles = new[]
        {
            ProjectileAt(new Vector2(1f, 2f), new Vector2(50f, 0f)),
            ProjectileAt(new Vector2(-3f, 0f), new Vector2(0f, 30f)),
        };
        SimToWorldMapper.BuildProjectileTrail(projectiles).Should().HaveCount(8);
    }

    [Fact]
    public void BuildProjectileTrail_places_head_cube_at_sim_position_lifted_to_muzzle_height()
    {
        var snapshot = ProjectileAt(new Vector2(4f, 6f), Vector2.Zero);
        var trail = SimToWorldMapper.BuildProjectileTrail(new[] { snapshot });

        var head = trail[0].Translation;
        head.X.Should().BeApproximately(4f, Epsilon);
        head.Y.Should().BeApproximately(SimToWorldMapper.MuzzleHeightMeters, Epsilon);
        head.Z.Should().BeApproximately(-6f, Epsilon);
    }

    [Fact]
    public void BuildProjectileTrail_echoes_step_back_along_negated_velocity_in_world_xz()
    {
        var snapshot = ProjectileAt(new Vector2(0f, 0f), new Vector2(50f, 10f));
        var trail = SimToWorldMapper.BuildProjectileTrail(new[] { snapshot });

        // velocityXz = (50, 0, -10). echo k = head - velocityXz * (k * 0.05 s).
        for (var k = 0; k < SimToWorldMapper.ProjectileTrailEchoes; k++)
        {
            var pos = trail[k].Translation;
            pos.X.Should().BeApproximately(-50f * k * SimToWorldMapper.ProjectileTrailEchoSpacingSeconds, Epsilon);
            pos.Y.Should().BeApproximately(SimToWorldMapper.MuzzleHeightMeters, Epsilon);
            pos.Z.Should().BeApproximately(10f * k * SimToWorldMapper.ProjectileTrailEchoSpacingSeconds, Epsilon);
        }
    }

    [Fact]
    public void BuildProjectileTrail_zero_velocity_collapses_all_echoes_onto_the_head()
    {
        var snapshot = ProjectileAt(new Vector2(2f, 3f), Vector2.Zero);
        var trail = SimToWorldMapper.BuildProjectileTrail(new[] { snapshot });

        var head = trail[0].Translation;
        for (var k = 1; k < trail.Length; k++)
        {
            trail[k].Translation.Should().Be(head);
        }
    }

    [Fact]
    public void BuildProjectileTrail_steps_back_along_vertical_velocity()
    {
        var snapshot = ProjectileAt(new Vector2(0f, 0f), new Vector2(100f, 200f), verticalVelocityMps: 10f);
        var trail = SimToWorldMapper.BuildProjectileTrail(new[] { snapshot });

        for (var echo = 0; echo < trail.Length; echo++)
        {
            trail[echo].Translation.Y.Should().BeApproximately(
                SimToWorldMapper.MuzzleHeightMeters - (echo * 10f * SimToWorldMapper.ProjectileTrailEchoSpacingSeconds),
                Epsilon);
        }
    }

    [Fact]
    public void BuildProjectileTrail_uses_the_projectiles_captured_visual_height()
    {
        var snapshot = ProjectileAt(Vector2.Zero, Vector2.Zero, visualHeightMeters: 2.75f);

        var trail = SimToWorldMapper.BuildProjectileTrail(new[] { snapshot });

        trail.Should().OnlyContain(world => world.Translation.Y == 2.75f);
    }

    [Fact]
    public void BuildShellHeads_restores_physical_scale_and_points_the_nose_along_full_velocity()
    {
        var catalog = ContentShellVisualCsv.Parse(
            "ammo_type,model_vfs_path,canon_source\nAP,res://shell/pzgr39.glb,test");
        var snapshot = ProjectileAt(
            Vector2.Zero,
            new Vector2(0f, -10f),
            verticalVelocityMps: 10f);

        var world = SimToWorldMapper.BuildShellHeads(new[] { snapshot }, catalog)
            .Should().ContainSingle().Which;
        var nose = Vector3.TransformNormal(Vector3.UnitX, world);
        var direction = Vector3.Normalize(nose);

        nose.Length().Should().BeApproximately(ContentShellVisualGeometry.PzGr39DisplayScale, Epsilon);
        direction.X.Should().BeApproximately(0f, Epsilon);
        direction.Y.Should().BeApproximately(0.7071f, 1e-3f);
        direction.Z.Should().BeApproximately(0.7071f, 1e-3f);
    }

    private static ProjectileSnapshot ProjectileAt(
        Vector2 position,
        Vector2 velocity,
        float visualHeightMeters = SimToWorldMapper.MuzzleHeightMeters,
        float verticalVelocityMps = 0f) =>
        new(Id: 1, Position: position, Velocity: velocity, Family: AmmoType.AP, VisualHeightMeters: visualHeightMeters, VerticalVelocityMps: verticalVelocityMps);
}
