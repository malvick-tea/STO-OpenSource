using System;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Garupan.Client.Windows.Direct3D12.Composition.Models;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Ui;
using Xunit;

namespace Garupan.Client.Windows.Direct3D12.Tests.Composition.Models;

/// <summary>Pure-CPU coverage of <see cref="ModelScenePlan"/> — the device-free maths
/// that turns the screen-facing 3D contract into scene-renderer inputs. Runs in headless
/// CI (no GPU), unlike the <see cref="D3D12ModelRenderer"/> smoke which needs a device.</summary>
public sealed class ModelScenePlanTests
{
    [Fact]
    public void BuildCameras_sets_aspect_ratio_from_viewport_dimensions()
    {
        var camera = CameraView3D.LookAt(new Vector3(0f, 2f, 6f), Vector3.Zero, fovY: 45f);

        var cameras = ModelScenePlan.BuildCameras(camera, viewportWidth: 1600, viewportHeight: 900);

        cameras.Main.AspectRatio.Should().BeApproximately(1600f / 900f, 1e-5f);
    }

    [Fact]
    public void BuildCameras_converts_vertical_fov_from_degrees_to_radians()
    {
        var camera = CameraView3D.LookAt(new Vector3(0f, 0f, 5f), Vector3.Zero, fovY: 60f);

        var cameras = ModelScenePlan.BuildCameras(camera, 1280, 720);

        cameras.Main.FovYRadians.Should().BeApproximately(60f * MathF.PI / 180f, 1e-5f);
    }

    [Fact]
    public void BuildCameras_carries_position_through_and_derives_a_normalised_forward()
    {
        var position = new Vector3(3f, 4f, 12f);
        var target = new Vector3(3f, 4f, 0f);
        var camera = CameraView3D.LookAt(position, target, fovY: 45f);

        var main = ModelScenePlan.BuildCameras(camera, 1280, 720).Main;

        main.PositionWorld.Should().Be(position);
        main.ForwardWorld.Should().Be(Vector3.Normalize(target - position));
        main.ForwardWorld.Length().Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void BuildCameras_view_matrix_is_the_look_at_of_the_camera_basis()
    {
        var camera = CameraView3D.LookAt(new Vector3(0f, 3f, 8f), new Vector3(0f, 1f, 0f), fovY: 45f);

        var main = ModelScenePlan.BuildCameras(camera, 1280, 720).Main;

        main.View.Should().Be(Matrix4x4.CreateLookAt(camera.Position, camera.Target, camera.Up));
    }

    [Fact]
    public void BuildCameras_uses_a_consistent_near_and_far_plane()
    {
        var camera = CameraView3D.LookAt(new Vector3(0f, 0f, 5f), Vector3.Zero, fovY: 45f);

        var main = ModelScenePlan.BuildCameras(camera, 1280, 720).Main;

        main.NearPlane.Should().Be(0.1f);
        main.FarPlane.Should().Be(500f);
        main.NearPlane.Should().BeLessThan(main.FarPlane);
    }

    [Theory]
    [InlineData(0, 720)]
    [InlineData(1280, 0)]
    [InlineData(-1, 720)]
    [InlineData(1280, -8)]
    public void BuildCameras_rejects_degenerate_viewport_dimensions(int width, int height)
    {
        var camera = CameraView3D.LookAt(new Vector3(0f, 0f, 5f), Vector3.Zero, fovY: 45f);

        var act = () => ModelScenePlan.BuildCameras(camera, width, height);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ToTintFactor_maps_opaque_white_to_the_identity_factor()
    {
        ModelScenePlan.ToTintFactor(Color.White).Should().Be(Vector4.One);
    }

    [Fact]
    public void ToTintFactor_normalises_every_channel_from_bytes_to_the_unit_range()
    {
        var factor = ModelScenePlan.ToTintFactor(new Color(255, 128, 0, 64));

        factor.X.Should().Be(1f);
        factor.Y.Should().BeApproximately(128f / 255f, 1e-6f);
        factor.Z.Should().Be(0f);
        factor.W.Should().BeApproximately(64f / 255f, 1e-6f);
    }

    [Fact]
    public void FlattenNodeDraws_returns_an_empty_list_for_no_placements()
    {
        ModelScenePlan.FlattenNodeDraws(Array.Empty<ModelPlacement>()).Should().BeEmpty();
    }

    [Fact]
    public void FlattenNodeDraws_post_multiplies_each_template_world_by_the_placement_world()
    {
        var template = new[] { new SceneNodeDraw(7, Matrix4x4.CreateTranslation(1f, 0f, 0f)) };
        var placement = new ModelPlacement(template, Matrix4x4.CreateTranslation(0f, 5f, 0f), Vector4.One);

        var flat = ModelScenePlan.FlattenNodeDraws(new[] { placement });

        flat.Should().HaveCount(1);
        flat[0].MeshIndex.Should().Be(7);
        flat[0].World.Should().Be(Matrix4x4.CreateTranslation(1f, 5f, 0f));
    }

    [Fact]
    public void FlattenNodeDraws_multiplies_the_template_tint_by_the_placement_tint()
    {
        var template = new[] { new SceneNodeDraw(0, Matrix4x4.Identity, new Vector4(0.5f, 0.5f, 1f, 1f)) };
        var placement = new ModelPlacement(template, Matrix4x4.Identity, new Vector4(0.5f, 1f, 1f, 0.8f));

        var flat = ModelScenePlan.FlattenNodeDraws(new[] { placement });

        flat[0].TintFactor.Should().Be(new Vector4(0.25f, 0.5f, 1f, 0.8f));
    }

    [Fact]
    public void FlattenNodeDraws_concatenates_every_placement_in_placement_major_order()
    {
        var first = new[]
        {
            new SceneNodeDraw(1, Matrix4x4.Identity),
            new SceneNodeDraw(2, Matrix4x4.Identity),
        };
        var second = new[] { new SceneNodeDraw(3, Matrix4x4.Identity) };
        var placements = new[]
        {
            new ModelPlacement(first, Matrix4x4.Identity, Vector4.One),
            new ModelPlacement(second, Matrix4x4.Identity, Vector4.One),
        };

        var flat = ModelScenePlan.FlattenNodeDraws(placements);

        flat.Select(draw => draw.MeshIndex).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void FlattenNodeDraws_rejects_a_null_placement_list()
    {
        var act = () => ModelScenePlan.FlattenNodeDraws(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
