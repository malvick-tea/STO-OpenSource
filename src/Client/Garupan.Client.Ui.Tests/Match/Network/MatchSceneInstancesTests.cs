using System.Linq;
using System.Numerics;
using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Garupan.Sim.Components;
using Garupan.Sim.Terrain;
using Opus.Engine.Ui;
using Xunit;

namespace Garupan.Client.Ui.Tests.Match.Network;

/// <summary>
/// Pure coverage for <see cref="MatchSceneInstances"/>: a scene plan's tank placements
/// become world matrices (translation = world position) + tints (live = white, knocked
/// out = darkened), with the self flag carried through. No GPU.
/// </summary>
public sealed class MatchSceneInstancesTests
{
    private static readonly CameraView3D AnyCamera =
        CameraView3D.LookAt(new Vector3(0f, 10f, -10f), Vector3.Zero, 50f);

    [Fact]
    public void Each_placement_becomes_one_instance_translated_to_its_world_position()
    {
        var plan = new NetworkMatchScenePlan(AnyCamera, new[]
        {
            new TankPlacement(new Vector3(3f, 0f, 5f), 0f, IsSelf: true, KnockedOut: false),
            new TankPlacement(new Vector3(-4f, 0f, 8f), 0f, IsSelf: false, KnockedOut: false),
        });

        var instances = MatchSceneInstances.From(plan);

        instances.Should().HaveCount(2);
        instances[0].World.Translation.Should().Be(new Vector3(3f, 0f, 5f));
        instances[1].World.Translation.Should().Be(new Vector3(-4f, 0f, 8f));
    }

    [Fact]
    public void The_self_flag_is_carried_through()
    {
        var plan = new NetworkMatchScenePlan(AnyCamera, new[]
        {
            new TankPlacement(Vector3.Zero, 0f, IsSelf: false, KnockedOut: false),
            new TankPlacement(Vector3.Zero, 0f, IsSelf: true, KnockedOut: false),
        });

        var instances = MatchSceneInstances.From(plan);

        instances[0].IsSelf.Should().BeFalse();
        instances[1].IsSelf.Should().BeTrue();
    }

    [Fact]
    public void Positive_sim_yaw_maps_model_forward_from_plus_x_toward_plus_z()
    {
        var plan = new NetworkMatchScenePlan(AnyCamera, new[]
        {
            new TankPlacement(Vector3.Zero, System.MathF.PI / 2f, IsSelf: true, KnockedOut: false),
        });

        var forward = Vector3.TransformNormal(Vector3.UnitZ, MatchSceneInstances.From(plan)[0].World);

        forward.X.Should().BeApproximately(0f, 1e-3f);
        forward.Z.Should().BeApproximately(1f, 1e-3f);
    }

    [Fact]
    public void Zero_sim_yaw_aligns_the_obj_models_authored_plus_z_forward_to_plus_x()
    {
        var plan = new NetworkMatchScenePlan(AnyCamera, new[]
        {
            new TankPlacement(Vector3.Zero, 0f, IsSelf: true, KnockedOut: false),
        });

        var forward = Vector3.TransformNormal(Vector3.UnitZ, MatchSceneInstances.From(plan)[0].World);

        forward.X.Should().BeApproximately(1f, 1e-3f);
        forward.Z.Should().BeApproximately(0f, 1e-3f);
    }

    [Fact]
    public void A_live_tank_is_untinted_white()
    {
        var plan = new NetworkMatchScenePlan(AnyCamera, new[]
        {
            new TankPlacement(Vector3.Zero, 0f, IsSelf: false, KnockedOut: false),
        });

        MatchSceneInstances.From(plan)[0].Tint.Should().Be(Vector4.One);
    }

    [Fact]
    public void A_knocked_out_tank_is_darkened()
    {
        var plan = new NetworkMatchScenePlan(AnyCamera, new[]
        {
            new TankPlacement(Vector3.Zero, 0f, IsSelf: false, KnockedOut: true),
        });

        var tint = MatchSceneInstances.From(plan)[0].Tint;
        tint.Should().NotBe(Vector4.One);
        new[] { tint.X, tint.Y, tint.Z }.Should().OnlyContain(c => c < 1f, "a wreck is darker than white");
    }

    [Fact]
    public void A_moving_shell_lies_flat_with_its_nose_along_its_velocity()
    {
        var plan = new NetworkMatchScenePlan(AnyCamera, System.Array.Empty<TankPlacement>())
        {
            Projectiles = new[] { new ProjectilePlacement(new Vector3(5f, 1.5f, 7f), new Vector3(0f, 0f, 9f)) },
        };

        var world = MatchSceneInstances.ProjectileWorlds(plan).Should().ContainSingle().Which;

        world.Translation.Should().Be(new Vector3(5f, 1.5f, 7f));

        // The shell's authored nose (+X) points exactly along velocity. Pure +Z velocity
        // therefore requires a +Z nose with no vertical tilt.
        var nose = Vector3.TransformNormal(Vector3.UnitX, world);
        nose.Y.Should().BeApproximately(0f, 1e-3f, "a real shell flies lying down, never vertically");
        var direction = Vector3.Normalize(nose);
        direction.X.Should().BeApproximately(0f, 1e-3f);
        direction.Z.Should().BeApproximately(1f, 1e-3f);
        nose.Length().Should().BeApproximately(
            MatchSceneInstances.ShellDisplayScale, 1e-3f, "the shell is uniformly scaled for visibility");
    }

    [Fact]
    public void A_stationary_shell_keeps_its_nose_on_plus_x_at_its_position()
    {
        var plan = new NetworkMatchScenePlan(AnyCamera, System.Array.Empty<TankPlacement>())
        {
            Projectiles = new[] { new ProjectilePlacement(new Vector3(-2f, 1.5f, 3f), Vector3.Zero) },
        };

        var world = MatchSceneInstances.ProjectileWorlds(plan).Should().ContainSingle().Which;

        world.Translation.Should().Be(new Vector3(-2f, 1.5f, 3f));
        var direction = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitX, world));
        direction.X.Should().BeApproximately(1f, 1e-3f, "zero velocity leaves the shell nose on +X");
    }

    [Fact]
    public void A_climbing_shell_tilts_its_nose_along_the_full_velocity()
    {
        var plan = new NetworkMatchScenePlan(AnyCamera, System.Array.Empty<TankPlacement>())
        {
            Projectiles = new[] { new ProjectilePlacement(Vector3.Zero, new Vector3(10f, 10f, 0f)) },
        };

        var world = MatchSceneInstances.ProjectileWorlds(plan).Should().ContainSingle().Which;
        var direction = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitX, world));

        direction.X.Should().BeApproximately(0.7071f, 1e-3f);
        direction.Y.Should().BeApproximately(0.7071f, 1e-3f);
    }

    [Fact]
    public void On_flat_terrain_a_hull_is_seated_at_the_surface_height_and_stays_level()
    {
        var terrain = new TerrainHeightField(2, 100f, new[] { 7f, 7f, 7f, 7f });
        var plan = new NetworkMatchScenePlan(AnyCamera, new[]
        {
            new TankPlacement(new Vector3(10f, 0f, 20f), 0f, IsSelf: true, KnockedOut: false),
        });

        var world = MatchSceneInstances.From(plan, terrain)[0].World;

        world.Translation.X.Should().BeApproximately(10f, 1e-4f);
        world.Translation.Y.Should().BeApproximately(7f, 1e-4f, "the hull sits on the sampled surface, not Y=0");
        world.Translation.Z.Should().BeApproximately(20f, 1e-4f);
        var up = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, world));
        up.Y.Should().BeApproximately(1f, 1e-4f, "flat ground keeps the deck level");
    }

    [Fact]
    public void On_a_slope_a_hull_tilts_so_its_deck_lies_along_the_surface_normal()
    {
        // A 3x3 ramp climbing +10 m per cell toward +X; the surface normal there is
        // (-1, 1, 0)/sqrt(2). Every tank conforms through this one path, so the whole roster tilts.
        var ramp = new[] { 0f, 10f, 20f, 0f, 10f, 20f, 0f, 10f, 20f };
        var terrain = new TerrainHeightField(3, 20f, ramp);
        var plan = new NetworkMatchScenePlan(AnyCamera, new[]
        {
            new TankPlacement(Vector3.Zero, 0f, IsSelf: true, KnockedOut: false),
        });

        var world = MatchSceneInstances.From(plan, terrain)[0].World;

        world.Translation.Y.Should().BeApproximately(10f, 1e-3f, "seated on the mid-ramp height");
        var up = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, world));
        up.X.Should().BeApproximately(-System.MathF.Sqrt(0.5f), 1e-3f, "the deck leans back from the rise");
        up.Y.Should().BeApproximately(System.MathF.Sqrt(0.5f), 1e-3f);
        up.Z.Should().BeApproximately(0f, 1e-3f);
    }

    [Fact]
    public void A_fallen_pole_under_one_track_lifts_that_side_into_a_gentle_roll()
    {
        var surface = new DynamicFelledPropSurface((_, _) => 3f);

        // A pole lying along the hull's travel under the RIGHT track (z = +1.45 = the footprint half
        // width). The right corners ride its 0.3 m crest; the left stay on the base, so the hull rolls
        // a few degrees — not the capsize the old point-normal seating produced over the same bump.
        surface.Add(new FelledPropSurfaceMember(
            new Vector2(-4f, 1.45f), FallYawRadians: 0f, LengthMeters: 9f, RadiusMeters: 0.15f, PropState.Fallen));
        var plan = new NetworkMatchScenePlan(AnyCamera, new[]
        {
            new TankPlacement(Vector3.Zero, 0f, IsSelf: true, KnockedOut: false),
        });

        var world = MatchSceneInstances.From(plan, surface)[0].World;

        world.Translation.Y.Should().BeInRange(3f, 3.31f, "the lifted track raises the hull, but it bridges rather than perches");
        var up = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, world));
        up.Y.Should().BeGreaterThan(0.99f, "a felled pole under one track is a gentle roll, not a capsize");
    }

    [Fact]
    public void An_empty_plan_yields_no_instances()
    {
        var plan = new NetworkMatchScenePlan(AnyCamera, System.Array.Empty<TankPlacement>());

        MatchSceneInstances.From(plan).Should().BeEmpty();
    }
}
