using System;
using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Garupan.Client.Ui.Screens.Match;
using Xunit;

namespace Garupan.Client.Ui.Tests.Screens.Match;

public sealed class NetworkMatchCameraControlTests
{
    [Fact]
    public void Zero_mouse_delta_does_not_jump_the_camera()
    {
        var camera = new NetworkMatchCameraControl();

        camera.Update(0, 0, orbitHeld: true, mouseWheelDelta: 0f, deltaSeconds: 1d / 60d);

        camera.YawRadians.Should().Be(NetworkMatchSceneProjection.DefaultOrbitYawRadians);
        camera.PitchRadians.Should().Be(NetworkMatchSceneProjection.DefaultOrbitPitchRadians);
        camera.MouseMovedThisFrame.Should().BeFalse();
    }

    [Fact]
    public void Rmb_drag_orbits_the_camera()
    {
        var camera = StartedCamera();

        camera.Update(20, -10, orbitHeld: true, mouseWheelDelta: 0f, deltaSeconds: 1d / 60d);

        camera.YawRadians.Should().BeApproximately(
            NetworkMatchSceneProjection.DefaultOrbitYawRadians + (20f * NetworkMatchCameraControl.YawSensitivity),
            1e-5f);
        camera.PitchRadians.Should().BeApproximately(
            NetworkMatchSceneProjection.DefaultOrbitPitchRadians - (10f * NetworkMatchCameraControl.PitchSensitivity),
            1e-5f);
        camera.MouseMovedThisFrame.Should().BeTrue();
    }

    [Fact]
    public void Mouse_motion_without_rmb_does_not_orbit_the_camera()
    {
        var camera = StartedCamera();

        camera.Update(20, -10, orbitHeld: false, mouseWheelDelta: 0f, deltaSeconds: 1d / 60d);

        camera.YawRadians.Should().Be(NetworkMatchSceneProjection.DefaultOrbitYawRadians);
        camera.PitchRadians.Should().Be(NetworkMatchSceneProjection.DefaultOrbitPitchRadians);
        camera.MouseMovedThisFrame.Should().BeTrue();
    }

    [Fact]
    public void Mouse_motion_without_rmb_advances_and_holds_the_turret_target()
    {
        var camera = StartedCamera(currentTurretYawRadians: 0.25f);

        camera.Update(
            20,
            0,
            orbitHeld: false,
            mouseWheelDelta: 0f,
            deltaSeconds: 1d / 60d,
            currentTurretYawRadians: 0.25f);
        var targetAfterMouseMotion = camera.ResolveTurretTarget(fallbackYawRadians: 0f);

        camera.Update(
            0,
            0,
            orbitHeld: false,
            mouseWheelDelta: 0f,
            deltaSeconds: 1d / 60d,
            currentTurretYawRadians: 0.30f);

        targetAfterMouseMotion.Should().BeApproximately(
            0.25f + (20f * NetworkMatchCameraControl.TurretYawSensitivity),
            1e-5f);
        camera.ResolveTurretTarget(fallbackYawRadians: 0f).Should().Be(targetAfterMouseMotion);
    }

    [Fact]
    public void Rmb_drag_preserves_the_local_turret_target_when_the_snapshot_is_delayed()
    {
        var camera = StartedCamera(currentTurretYawRadians: 0.25f);
        camera.Update(
            20,
            0,
            orbitHeld: false,
            mouseWheelDelta: 0f,
            deltaSeconds: 1d / 60d,
            currentTurretYawRadians: 0.25f);

        camera.Update(
            40,
            0,
            orbitHeld: true,
            mouseWheelDelta: 0f,
            deltaSeconds: 1d / 60d,
            currentTurretYawRadians: 0.20f);

        camera.ResolveTurretTarget(fallbackYawRadians: 0f).Should().BeApproximately(
            0.25f + (20f * NetworkMatchCameraControl.TurretYawSensitivity),
            1e-5f);
    }

    [Fact]
    public void Mouse_drag_clamps_turret_target_lead_before_it_can_cross_the_antipode()
    {
        var camera = StartedCamera(currentTurretYawRadians: 0f);

        camera.Update(
            1000,
            0,
            orbitHeld: false,
            mouseWheelDelta: 0f,
            deltaSeconds: 1d / 60d,
            currentTurretYawRadians: 0f);

        camera.ResolveTurretTarget(fallbackYawRadians: 0f).Should().BeApproximately(
            NetworkMatchCameraControl.MaxTurretTargetLeadRadians,
            1e-5f);
    }

    [Fact]
    public void Releasing_rmb_returns_the_camera_toward_the_default_chase_view()
    {
        var camera = StartedCamera();
        camera.Update(80, 20, orbitHeld: true, mouseWheelDelta: 0f, deltaSeconds: 1d / 60d);
        var draggedYaw = camera.YawRadians;
        var draggedPitch = camera.PitchRadians;

        camera.Update(0, 0, orbitHeld: false, mouseWheelDelta: 0f, deltaSeconds: 0.5d);

        MathF.Abs(camera.YawRadians - NetworkMatchSceneProjection.DefaultOrbitYawRadians)
            .Should().BeLessThan(MathF.Abs(draggedYaw - NetworkMatchSceneProjection.DefaultOrbitYawRadians));
        MathF.Abs(camera.PitchRadians - NetworkMatchSceneProjection.DefaultOrbitPitchRadians)
            .Should().BeLessThan(MathF.Abs(draggedPitch - NetworkMatchSceneProjection.DefaultOrbitPitchRadians));
    }

    [Fact]
    public void Releasing_rmb_returns_the_camera_behind_the_current_barrel()
    {
        var camera = StartedCamera(currentTurretYawRadians: MathF.PI / 2f);
        camera.Update(
            80,
            0,
            orbitHeld: true,
            mouseWheelDelta: 0f,
            deltaSeconds: 1d / 60d,
            currentTurretYawRadians: MathF.PI / 2f);
        var draggedYaw = camera.YawRadians;

        camera.Update(
            0,
            0,
            orbitHeld: false,
            mouseWheelDelta: 0f,
            deltaSeconds: 0.5d,
            currentTurretYawRadians: 0f);

        AngleDistance(camera.YawRadians, MathF.PI)
            .Should().BeLessThan(AngleDistance(draggedYaw, MathF.PI));
    }

    [Fact]
    public void Wheel_zoom_is_tightly_clamped()
    {
        var camera = StartedCamera();

        camera.Update(0, 0, orbitHeld: false, mouseWheelDelta: 100f, deltaSeconds: 1d / 60d);
        camera.DistanceMeters.Should().Be(NetworkMatchSceneProjection.MinChaseDistanceMeters);

        camera.Update(0, 0, orbitHeld: false, mouseWheelDelta: -100f, deltaSeconds: 1d / 60d);
        camera.DistanceMeters.Should().Be(NetworkMatchSceneProjection.MaxChaseDistanceMeters);
    }

    [Fact]
    public void Mouse_motion_without_rmb_pitches_the_barrel()
    {
        var camera = StartedCamera();

        camera.Update(0, -20, orbitHeld: false, mouseWheelDelta: 0f, deltaSeconds: 1d / 60d);

        camera.BarrelPitchRadians.Should().BeApproximately(
            20f * NetworkMatchCameraControl.BarrelPitchSensitivity,
            1e-5f);
    }

    [Fact]
    public void Mouse_barrel_preview_clamps_to_the_current_tanks_mount_limits()
    {
        var camera = StartedCamera();

        camera.Update(
            0,
            -1000,
            orbitHeld: false,
            mouseWheelDelta: 0f,
            deltaSeconds: 1d / 60d,
            minBarrelPitchRadians: -0.1f,
            maxBarrelPitchRadians: 0.2f);

        camera.BarrelPitchRadians.Should().Be(0.2f);
    }

    private static NetworkMatchCameraControl StartedCamera(float? currentTurretYawRadians = null)
    {
        var camera = new NetworkMatchCameraControl();
        camera.Update(
            0,
            0,
            orbitHeld: false,
            mouseWheelDelta: 0f,
            deltaSeconds: 1d / 60d,
            currentTurretYawRadians: currentTurretYawRadians);
        return camera;
    }

    private static float AngleDistance(float a, float b)
    {
        var delta = a - b;
        while (delta > MathF.PI)
        {
            delta -= 2f * MathF.PI;
        }

        while (delta < -MathF.PI)
        {
            delta += 2f * MathF.PI;
        }

        return MathF.Abs(delta);
    }
}
