using System;
using System.Collections.Generic;
using System.Numerics;
using Garupan.Client.Ui.Match.Network;
using Opus.Foundation;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>Creates one local visual burst for each newly observed authoritative
/// projectile id. Snapshot repetition is harmless and launch origin stays fixed while
/// the projectile continues downrange.</summary>
internal sealed class ShotVfxTracker
{
    // Cordite smoke lingers and drifts for a couple of seconds after the shot, well past the
    // sub-second flame; the renderer fades the cloud out across this whole window.
    public const float LifetimeSeconds = 2.2f;

    private const float TickSeconds = 1f / GameTime.DefaultTickRateHz;
    private const float GasForwardSpeedMetersPerSecond = 11f;
    private const float GasForwardDragPerSecond = 5f;

    // Buoyancy accelerates the cloud upward, then caps at a gentle terminal rise so the column
    // hangs and drifts over the long lifetime instead of rocketing tens of metres skyward.
    private const float GasBuoyancyMetersPerSecondSquared = 3.2f;
    private const float GasTerminalRiseMetersPerSecond = 1.6f;

    private const float FlashForwardSpeedMetersPerSecond = 3.5f;

    private readonly HashSet<int> _activeProjectileIds = new();
    private readonly List<ShotVfxBurst> _bursts = new();
    private long? _lastSnapshotTick;

    public IReadOnlyList<ShotVfxBurst> Resolve(NetworkMatchScenePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (_lastSnapshotTick == plan.SnapshotTick)
        {
            return _bursts;
        }

        if (_lastSnapshotTick is { } lastTick && plan.SnapshotTick < lastTick)
        {
            _activeProjectileIds.Clear();
            _bursts.Clear();
            _lastSnapshotTick = null;
        }

        if (_lastSnapshotTick is { } previousTick)
        {
            var elapsedSeconds = (plan.SnapshotTick - previousTick) * TickSeconds;
            for (var i = _bursts.Count - 1; i >= 0; i--)
            {
                var aged = _bursts[i] with { AgeSeconds = _bursts[i].AgeSeconds + elapsedSeconds };
                if (aged.AgeSeconds >= LifetimeSeconds)
                {
                    _bursts.RemoveAt(i);
                }
                else
                {
                    _bursts[i] = aged;
                }
            }
        }

        var currentIds = new HashSet<int>();
        for (var i = 0; i < plan.Projectiles.Count; i++)
        {
            var projectile = plan.Projectiles[i];
            currentIds.Add(projectile.Id);
            if (_activeProjectileIds.Add(projectile.Id))
            {
                _bursts.Add(new ShotVfxBurst(
                    projectile.LaunchPosition,
                    new Vector3(projectile.LaunchPosition.X, 0f, projectile.LaunchPosition.Z),
                    LaunchGasVelocity(projectile.Velocity),
                    AgeSeconds: 0f));
            }
        }

        _activeProjectileIds.IntersectWith(currentIds);
        _lastSnapshotTick = plan.SnapshotTick;
        return _bursts;
    }

    /// <summary>Ballistic gas starts along the barrel, loses forward speed rapidly, then
    /// buoyancy wins and carries the cloud upward.</summary>
    internal static Vector3 GasPosition(in ShotVfxBurst burst, float ageSeconds)
    {
        var age = Math.Clamp(ageSeconds, 0f, LifetimeSeconds);
        var draggedSeconds = (1f - MathF.Exp(-GasForwardDragPerSecond * age)) / GasForwardDragPerSecond;
        return burst.MuzzlePosition +
            (burst.InitialGasVelocity * draggedSeconds) +
            (Vector3.UnitY * BuoyantRise(age));
    }

    /// <summary>Upward travel of the cloud: quadratic acceleration under buoyancy until it reaches
    /// <see cref="GasTerminalRiseMetersPerSecond"/>, then a steady terminal rise. Keeps the long-lived
    /// cloud hanging near the muzzle rather than accelerating without bound.</summary>
    private static float BuoyantRise(float age)
    {
        var accelerationTime = GasTerminalRiseMetersPerSecond / GasBuoyancyMetersPerSecondSquared;
        if (age <= accelerationTime)
        {
            return 0.5f * GasBuoyancyMetersPerSecondSquared * age * age;
        }

        var riseAtTerminal = 0.5f * GasTerminalRiseMetersPerSecond * accelerationTime;
        return riseAtTerminal + (GasTerminalRiseMetersPerSecond * (age - accelerationTime));
    }

    /// <summary>Places the short-lived flame plume along the barrel axis. A muzzle flash
    /// is several overlapping sprites laid outward from the muzzle, not one upright puff.</summary>
    internal static Vector3 FlashPlumePosition(in ShotVfxBurst burst, float ageSeconds, float offsetMeters)
    {
        var direction = Vector3.Normalize(burst.InitialGasVelocity);
        return burst.MuzzlePosition +
            (direction * (Math.Max(0f, offsetMeters) + (Math.Max(0f, ageSeconds) * FlashForwardSpeedMetersPerSecond)));
    }

    private static Vector3 LaunchGasVelocity(Vector3 projectileVelocity)
    {
        var lengthSquared = projectileVelocity.LengthSquared();
        return lengthSquared > float.Epsilon
            ? Vector3.Normalize(projectileVelocity) * GasForwardSpeedMetersPerSecond
            : Vector3.UnitX * GasForwardSpeedMetersPerSecond;
    }
}

internal readonly record struct ShotVfxBurst(
    Vector3 MuzzlePosition,
    Vector3 DustPosition,
    Vector3 InitialGasVelocity,
    float AgeSeconds);
