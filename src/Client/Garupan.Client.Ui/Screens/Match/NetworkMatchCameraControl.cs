using System;
using Garupan.Client.Ui.Match.Network;
using Garupan.Sim.Snapshot;

namespace Garupan.Client.Ui.Screens.Match;

/// <summary>Owns local mouse controls: relative turret aim, RMB orbit, release-to-return,
/// and tightly-clamped mouse-wheel zoom. Pure state keeps the interaction headless-testable.</summary>
internal sealed class NetworkMatchCameraControl
{
    internal const float YawSensitivity = 0.006f;
    internal const float PitchSensitivity = 0.005f;

    /// <summary>Radians of commanded turret yaw per pixel of mouse travel. Deliberately low —
    /// the gun is a precision instrument: a comfortable ~90° lay takes a full ~700 px sweep, so a
    /// nudge nudges the gun rather than slewing it across the field. Lower than the camera-orbit
    /// <see cref="YawSensitivity"/> because aiming the cannon wants finer control than swinging the
    /// chase view.</summary>
    internal const float TurretYawSensitivity = 0.0022f;

    /// <summary>Radians of commanded barrel elevation per pixel of mouse travel. Lower still than
    /// the yaw gain — the elevation arc is only a few degrees wide, so coarse steps would make it
    /// impossible to settle on a target's hull.</summary>
    internal const float BarrelPitchSensitivity = 0.0016f;

    /// <summary>How far the locally-maintained aim target may lead the authoritative turret before
    /// the drag is clamped. The real turret traverses slowly (historical ~14°/s), so an unbounded
    /// lead let a single flick queue a near-half-rotation that kept grinding long after the mouse
    /// stopped — read by the player as "I barely moved and the turret ran away". Bounding the lead
    /// to ~63° keeps the commanded target close to where the gun actually is: the turret tracks the
    /// mouse instead of chasing a far-ahead phantom. Still wide of the antipode so a delayed
    /// snapshot can never flip a continuing drag into the opposite direction.</summary>
    internal const float MaxTurretTargetLeadRadians = MathF.PI * 0.35f;
    internal const float MinPitchRadians = 0.12f;
    internal const float MaxPitchRadians = 1.4f;
    internal const float ZoomMetersPerWheelTick = 1.25f;
    internal const float ReturnSharpness = 5f;

    private bool _hasTurretTarget;
    private float _turretTargetYawRadians;

    public float YawRadians { get; private set; } = NetworkMatchSceneProjection.DefaultOrbitYawRadians;

    public float PitchRadians { get; private set; } = NetworkMatchSceneProjection.DefaultOrbitPitchRadians;

    public float DistanceMeters { get; private set; } = NetworkMatchSceneProjection.ChaseDistanceMeters;

    public float BarrelPitchRadians { get; private set; }

    public bool MouseMovedThisFrame { get; private set; }

    public void Update(
        int mouseDeltaX,
        int mouseDeltaY,
        bool orbitHeld,
        float mouseWheelDelta,
        double deltaSeconds,
        float? currentTurretYawRadians = null,
        float minBarrelPitchRadians = EntitySnapshot.UnboundedMinBarrelPitchRadians,
        float maxBarrelPitchRadians = EntitySnapshot.UnboundedMaxBarrelPitchRadians)
    {
        if (!_hasTurretTarget && currentTurretYawRadians is { } initialTurretYaw && float.IsFinite(initialTurretYaw))
        {
            _turretTargetYawRadians = WrapSignedRadians(initialTurretYaw);
            _hasTurretTarget = true;
        }

        MouseMovedThisFrame = mouseDeltaX != 0 || mouseDeltaY != 0;
        if (orbitHeld)
        {
            YawRadians += mouseDeltaX * YawSensitivity;
            PitchRadians = Math.Clamp(
                PitchRadians + (mouseDeltaY * PitchSensitivity),
                MinPitchRadians,
                MaxPitchRadians);
        }
        else
        {
            ReturnBehindTurret(currentTurretYawRadians, (float)Math.Max(0d, deltaSeconds));
            ApplyTurretAim(mouseDeltaX, currentTurretYawRadians);
            ApplyBarrelPitch(mouseDeltaY, minBarrelPitchRadians, maxBarrelPitchRadians);
        }

        if (float.IsFinite(mouseWheelDelta))
        {
            DistanceMeters = Math.Clamp(
                DistanceMeters - (mouseWheelDelta * ZoomMetersPerWheelTick),
                NetworkMatchSceneProjection.MinChaseDistanceMeters,
                NetworkMatchSceneProjection.MaxChaseDistanceMeters);
        }
    }

    public float ResolveTurretTarget(float fallbackYawRadians) =>
        _hasTurretTarget ? _turretTargetYawRadians : fallbackYawRadians;

    private void ApplyTurretAim(int mouseDeltaX, float? currentTurretYawRadians)
    {
        if (mouseDeltaX == 0)
        {
            return;
        }

        if (!_hasTurretTarget)
        {
            _turretTargetYawRadians = currentTurretYawRadians is { } initialCurrent && float.IsFinite(initialCurrent)
                ? WrapSignedRadians(initialCurrent)
                : 0f;
            _hasTurretTarget = true;
        }

        var requestedDelta = mouseDeltaX * TurretYawSensitivity;
        if (currentTurretYawRadians is { } currentSnapshot && float.IsFinite(currentSnapshot))
        {
            // The wire command is an absolute yaw and the server follows its shortest arc.
            // Keep relative mouse aim inside the antipode so delayed snapshots cannot make
            // a continuing drag suddenly command the opposite direction.
            var lead = WrapSignedRadians(_turretTargetYawRadians - currentSnapshot);
            _turretTargetYawRadians = WrapSignedRadians(
                currentSnapshot + Math.Clamp(
                    lead + requestedDelta,
                    -MaxTurretTargetLeadRadians,
                    MaxTurretTargetLeadRadians));
            return;
        }

        _turretTargetYawRadians = WrapSignedRadians(_turretTargetYawRadians + requestedDelta);
    }

    private void ApplyBarrelPitch(int mouseDeltaY, float minPitchRadians, float maxPitchRadians)
    {
        if (!float.IsFinite(minPitchRadians)
            || !float.IsFinite(maxPitchRadians)
            || minPitchRadians > maxPitchRadians)
        {
            minPitchRadians = EntitySnapshot.UnboundedMinBarrelPitchRadians;
            maxPitchRadians = EntitySnapshot.UnboundedMaxBarrelPitchRadians;
        }

        BarrelPitchRadians = Math.Clamp(
            BarrelPitchRadians - (mouseDeltaY * BarrelPitchSensitivity),
            minPitchRadians,
            maxPitchRadians);
    }

    private void ReturnBehindTurret(float? currentTurretYawRadians, float deltaSeconds)
    {
        var returnYaw = currentTurretYawRadians is { } turretYaw && float.IsFinite(turretYaw)
            ? WrapSignedRadians(turretYaw + MathF.PI)
            : NetworkMatchSceneProjection.DefaultOrbitYawRadians;
        var amount = 1f - MathF.Exp(-ReturnSharpness * deltaSeconds);
        YawRadians = LerpAngle(YawRadians, returnYaw, amount);
        PitchRadians += (NetworkMatchSceneProjection.DefaultOrbitPitchRadians - PitchRadians) * amount;
    }

    private static float LerpAngle(float from, float to, float amount) =>
        from + (WrapSignedRadians(to - from) * amount);

    private static float WrapSignedRadians(float radians)
    {
        while (radians > MathF.PI)
        {
            radians -= 2f * MathF.PI;
        }

        while (radians < -MathF.PI)
        {
            radians += 2f * MathF.PI;
        }

        return radians;
    }
}
