using System;
using System.Collections.Generic;
using System.Numerics;
using Garupan.Sim.Snapshot;

namespace Garupan.Garage.Demo;

/// <summary>One alive tank as observed by the casing ejector — sim-XY position, sim yaw,
/// and an alive flag. Knocked-out tanks pass <see cref="IsAlive"/> = false and are
/// skipped by <see cref="CasingEjector.Update"/>'s shooter-assignment scan, so a tank
/// that just KO'd doesn't keep ejecting casings on its way to the floor.</summary>
public readonly record struct TankPose(Vector2 PositionXY, float SimYawRadians, bool IsAlive);

/// <summary>Demo-side casing ejector. Spawns one tumbling cylinder per newly observed
/// projectile, assigns the casing to the nearest alive tank within
/// <see cref="CasingEjectorConfig.ShooterAssignmentRadiusMeters"/>, and integrates each
/// casing under gravity until <see cref="CasingEjectorConfig.LifetimeSeconds"/> elapse.
/// Pure presentation state — does NOT live in Sim or snapshot, does NOT affect replay
/// determinism, does NOT couple to the wire codec.</summary>
/// <remarks>
/// Lifecycle: construct once at match start, call <see cref="Update"/> every frame with
/// the latest projectile snapshot + tank poses, read back <see cref="CasingMatrices"/>
/// for the renderer. Call <see cref="Reset"/> on match restart to drop live casings +
/// clear the tracked-id set. Determinism: spawn position + velocity are pure functions
/// of (shooter pose, config) — no randomness — so identical inputs produce identical
/// matrices across runs, even though the casing visual itself is not replay-bound.
/// </remarks>
public sealed class CasingEjector
{
    private readonly CasingEjectorConfig _config;
    private readonly HashSet<int> _trackedProjectileIds = new();
    private readonly List<CasingState> _casings = new();
    private readonly List<Matrix4x4> _matrices = new();

    public CasingEjector()
        : this(CasingEjectorConfig.Default)
    {
    }

    public CasingEjector(CasingEjectorConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    /// <summary>Number of live casings tracked this frame.</summary>
    public int LiveCount => _casings.Count;

    /// <summary>One world matrix per live casing, refreshed at the end of every
    /// <see cref="Update"/>. Hosts feed this into
    /// <see cref="Engine.Renderer.Direct3D12.Scene.GarageSceneController.CasingProjectiles"/>.</summary>
    public IReadOnlyList<Matrix4x4> CasingMatrices => _matrices;

    /// <summary>Drops all live casings + clears the tracked-id set. Call on match restart
    /// so the next match doesn't inherit casing state from the previous run.</summary>
    public void Reset()
    {
        _casings.Clear();
        _trackedProjectileIds.Clear();
        _matrices.Clear();
    }

    public void Update(
        IReadOnlyList<ProjectileSnapshot> projectiles,
        IReadOnlyList<TankPose> tanks,
        float deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(projectiles);
        ArgumentNullException.ThrowIfNull(tanks);
        if (!IsFiniteNonNegative(deltaSeconds))
        {
            return;
        }

        IntegrateLiveCasings(deltaSeconds);
        SpawnCasingsForNewProjectiles(projectiles, tanks);
        SyncTrackedIds(projectiles);
        RebuildMatricesCache();
    }

    private static bool IsFiniteNonNegative(float deltaSeconds) =>
        float.IsFinite(deltaSeconds) && deltaSeconds >= 0f;

    private void IntegrateLiveCasings(float deltaSeconds)
    {
        if (_casings.Count == 0)
        {
            return;
        }

        for (var i = _casings.Count - 1; i >= 0; i--)
        {
            var c = _casings[i];
            c.RemainingSeconds -= deltaSeconds;
            if (c.RemainingSeconds <= 0f)
            {
                _casings.RemoveAt(i);
                continue;
            }

            c.Velocity += _config.GravityMps2 * deltaSeconds;
            c.Position += c.Velocity * deltaSeconds;
            c.Rotation = Quaternion.Normalize(c.Rotation * BuildTumbleStep(_config.TumbleAngularVelocityRadPerSec, deltaSeconds));
            _casings[i] = c;
        }
    }

    private void SpawnCasingsForNewProjectiles(
        IReadOnlyList<ProjectileSnapshot> projectiles,
        IReadOnlyList<TankPose> tanks)
    {
        for (var i = 0; i < projectiles.Count; i++)
        {
            var p = projectiles[i];
            if (_trackedProjectileIds.Contains(p.Id))
            {
                continue;
            }

            var shooter = FindShooter(p.Position, tanks);
            if (shooter is null)
            {
                continue;
            }

            _casings.Add(BuildSpawnedCasing(shooter.Value, _config));
        }
    }

    private void SyncTrackedIds(IReadOnlyList<ProjectileSnapshot> projectiles)
    {
        _trackedProjectileIds.Clear();
        for (var i = 0; i < projectiles.Count; i++)
        {
            _trackedProjectileIds.Add(projectiles[i].Id);
        }
    }

    private void RebuildMatricesCache()
    {
        _matrices.Clear();
        for (var i = 0; i < _casings.Count; i++)
        {
            var c = _casings[i];
            _matrices.Add(Matrix4x4.CreateFromQuaternion(c.Rotation) * Matrix4x4.CreateTranslation(c.Position));
        }
    }

    private TankPose? FindShooter(Vector2 projectilePosition, IReadOnlyList<TankPose> tanks)
    {
        var radiusSquared = _config.ShooterAssignmentRadiusMeters * _config.ShooterAssignmentRadiusMeters;
        TankPose? closest = null;
        var closestDistanceSquared = float.MaxValue;
        for (var i = 0; i < tanks.Count; i++)
        {
            var t = tanks[i];
            if (!t.IsAlive)
            {
                continue;
            }

            var ds = Vector2.DistanceSquared(t.PositionXY, projectilePosition);
            if (ds > radiusSquared || ds >= closestDistanceSquared)
            {
                continue;
            }

            closest = t;
            closestDistanceSquared = ds;
        }

        return closest;
    }

    private static CasingState BuildSpawnedCasing(TankPose shooter, CasingEjectorConfig config)
    {
        var worldYaw = -shooter.SimYawRadians;
        var forward = new Vector3(MathF.Cos(worldYaw), 0f, -MathF.Sin(worldYaw));
        var rear = -forward;
        var groundPos = new Vector3(shooter.PositionXY.X, 0f, -shooter.PositionXY.Y);
        var spawnPos = groundPos + (rear * config.EjectionRearOffsetMeters) + (Vector3.UnitY * config.EjectionHeightMeters);
        var spawnVelocity = (rear * config.EjectionRearSpeedMps) + (Vector3.UnitY * config.EjectionUpwardSpeedMps);
        return new CasingState
        {
            Position = spawnPos,
            Velocity = spawnVelocity,
            Rotation = Quaternion.Identity,
            RemainingSeconds = config.LifetimeSeconds,
        };
    }

    private static Quaternion BuildTumbleStep(Vector3 angularVelocityRadPerSec, float deltaSeconds)
    {
        var magnitude = angularVelocityRadPerSec.Length();
        if (magnitude < float.Epsilon)
        {
            return Quaternion.Identity;
        }

        var axis = angularVelocityRadPerSec / magnitude;
        return Quaternion.CreateFromAxisAngle(axis, magnitude * deltaSeconds);
    }

    private struct CasingState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public Quaternion Rotation;
        public float RemainingSeconds;
    }
}
