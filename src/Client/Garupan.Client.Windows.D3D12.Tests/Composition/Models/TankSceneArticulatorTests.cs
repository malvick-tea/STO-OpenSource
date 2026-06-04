using System;
using System.IO;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Garupan.Client.Windows.Direct3D12.Composition.Models;
using Opus.Content.Meshes;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Foundation.Geometry;
using Xunit;

namespace Garupan.Client.Windows.Direct3D12.Tests.Composition.Models;

public sealed class TankSceneArticulatorTests
{
    private const string TankRelativePath = "content/tanks/vehicle_medium_b-rigged.glb";

    [Fact]
    public void Rigged_asset_poses_turret_wheels_and_left_track_uv()
    {
        var scene = ReadTankScene();
        var template = BuildTemplate(scene);
        var articulator = new TankSceneArticulator(scene, template);
        var tank = TankAt(barrelPitchRadians: 0.2f);

        var posed = articulator.BuildDraws(
            tank,
            Matrix4x4.Identity,
            Vector4.One,
            new TankMotion(LeftTravelMeters: 0.625f, RightTravelMeters: 0f));

        DrawFor(scene, template, "vehicle#bone_turret_63").World
            .Should().NotBe(DrawFor(scene, posed, "vehicle#bone_turret_63").World);
        CentreOf(scene, DrawFor(scene, posed, "vehicle#gun_barrel_70")).Y
            .Should().BeGreaterThan(CentreOf(scene, DrawFor(scene, template, "vehicle#gun_barrel_70")).Y);
        var authoredWheel = DrawFor(scene, template, "vehicle#wheel_l_01_98");
        var posedWheel = DrawFor(scene, posed, "vehicle#wheel_l_01_98");
        posedWheel.World.Should().NotBe(authoredWheel.World);
        Vector3.Distance(CentreOf(scene, authoredWheel), CentreOf(scene, posedWheel))
            .Should().BeLessThan(1e-4f, "a wheel must spin around its exported centre pivot");
        DrawFor(scene, posed, "vehicle#track_l_9").UvOffset.Y
            .Should().BeApproximately(0.5f, 1e-5f);
        DrawFor(scene, posed, "vehicle#track_r_10").UvOffset
            .Should().Be(Vector2.Zero);
        DrawFor(scene, posed, "vehicle#track_l_9").TintFactor
            .Should().Be(new Vector4(0.42f, 0.42f, 0.42f, 1f));
        DrawFor(scene, posed, "vehicle#bone_turret_63").TintFactor
            .Should().Be(Vector4.One);
    }

    [Fact]
    public void Barrel_pitch_keeps_the_exported_base_pivot_fixed()
    {
        var rotation = TankSceneArticulator.BarrelPitchRotation(0.3f);
        rotation.Translation.Should().Be(
            Vector3.Zero,
            "the GLB already owns the barrel-base pivot; a runtime translation makes the gun slide vertically");

        // A point one metre down the bore rises on elevation and drops on depression.
        var muzzle = new Vector3(0f, 0f, 1f);
        Vector3.Transform(muzzle, rotation).Y
            .Should().BeGreaterThan(muzzle.Y, "positive barrel pitch raises the muzzle");
        Vector3.Transform(muzzle, TankSceneArticulator.BarrelPitchRotation(-0.15f)).Y
            .Should().BeLessThan(muzzle.Y, "negative barrel pitch drops the muzzle");
    }

    [Fact]
    public void Gun_recoil_moves_the_assembly_backward_along_the_pitched_bore()
    {
        var level = TankSceneArticulator.BarrelPose(0f, 0.25f);
        var elevated = TankSceneArticulator.BarrelPose(0.3f, 0.25f);

        level.Translation.Z.Should().BeApproximately(-0.25f, 1e-5f);
        elevated.Translation.Z.Should().BeLessThan(0f);
        elevated.Translation.Y.Should().BeLessThan(0f, "an elevated barrel recoils backward and down along its bore");
    }

    [Fact]
    public void Barrel_and_front_mask_pitch_together_while_the_coax_stays_on_the_turret()
    {
        var scene = ReadTankScene();
        var articulator = new TankSceneArticulator(scene, BuildTemplate(scene));
        var motion = new TankMotion(LeftTravelMeters: 0f, RightTravelMeters: 0f);

        var level = articulator.BuildDraws(TankAt(0f), Matrix4x4.Identity, Vector4.One, motion);
        var elevated = articulator.BuildDraws(TankAt(0.3f), Matrix4x4.Identity, Vector4.One, motion);

        // The tube and broad front mask pitch around the trunnions seated inside the
        // turret face. The pivot must not be the rear edge of the tube.
        DrawFor(scene, elevated, "vehicle#gun_barrel_70").World
            .Should().NotBe(DrawFor(scene, level, "vehicle#gun_barrel_70").World);
        DrawFor(scene, elevated, "vehicle#bone_gun_69").World
            .Should().NotBe(DrawFor(scene, level, "vehicle#bone_gun_69").World);

        // ...but the coaxial MG is seated low on the turret face: it only yaws, so elevation
        // must not move it (the reported "static texture rides up with the barrel" artefact).
        DrawFor(scene, elevated, "vehicle#bone_mg_gun_twin_71").World
            .Should().Be(DrawFor(scene, level, "vehicle#bone_mg_gun_twin_71").World);
    }

    private static SceneNodeDraw[] BuildTemplate(GltfScene scene)
    {
        var worlds = SceneTreeMath.ComputeWorldTransforms(scene);
        return scene.Nodes
            .Select((node, index) => (node, index))
            .Where(pair => pair.node.MeshIndex is not null)
            .Select(pair => new SceneNodeDraw(
                pair.node.MeshIndex!.Value,
                worlds[pair.index],
                Vector4.One,
                Vector2.Zero,
                pair.index))
            .ToArray();
    }

    private static TankPlacement TankAt(float barrelPitchRadians) => new(
        Vector3.Zero,
        HullYawRadians: 0f,
        IsSelf: true,
        KnockedOut: false,
        EntityId: 7,
        TurretYawRadians: MathF.PI / 2f,
        BarrelPitchRadians: barrelPitchRadians);

    private static GltfScene ReadTankScene()
    {
        var path = Path.Combine(AppContext.BaseDirectory, TankRelativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue($"the test output should contain {TankRelativePath}");
        return GltfBinaryReader.ReadScene(File.ReadAllBytes(path));
    }

    private static SceneNodeDraw DrawFor(
        GltfScene scene,
        System.Collections.Generic.IReadOnlyList<SceneNodeDraw> draws,
        string nodeName)
    {
        var nodeIndex = Array.FindIndex(scene.Nodes, node => string.Equals(node.Name, nodeName, StringComparison.Ordinal));
        nodeIndex.Should().BeGreaterThanOrEqualTo(0, $"the rig should contain node '{nodeName}'");
        return draws.Single(draw => draw.NodeIndex == nodeIndex);
    }

    private static Vector3 CentreOf(GltfScene scene, SceneNodeDraw draw)
    {
        var positions = scene.Meshes[draw.MeshIndex].Primitives
            .SelectMany(primitive => primitive.Geometry.Positions)
            .ToArray();
        return Aabb.FromPoints(positions).Transform(draw.World).Centre;
    }
}
