using System;
using System.Collections.Generic;
using System.Numerics;
using Garupan.Sim.Snapshot;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Match.Network;

/// <summary>
/// Pure projection from a <see cref="WorldSnapshot"/> into a <see cref="NetworkMatchScenePlan"/>:
/// the chase camera that frames the local tank and the ground-plane placement of every
/// tank in the snapshot. No ECS, no GPU, no engine model handles — just the camera +
/// coordinate maths — so it is fully unit-testable headless.
/// </summary>
/// <remarks>
/// <para>
/// Coordinate convention: the sim's world is 2D — <see cref="EntitySnapshot.Position"/> is
/// (X = east, Y = north) and <see cref="EntitySnapshot.YawRadians"/> is measured CCW from
/// +X (east). The 3D scene is right-handed Y-up: world (X, Y) maps to the ground plane
/// (X, 0, Y) with +Y up, and a hull yaw θ points along (cos θ, 0, sin θ).
/// </para>
/// <para>
/// The camera chases the local tank from behind and above. When the local tank is not in
/// the snapshot (the first frame before the server's broadcast, spectating, or a peer
/// with no row yet) the projection falls back to a fixed overview looking down the field
/// so the player still sees the battle. This is the dev-validation framing; the Pillar-2
/// first-person gunsight camera graduates onto the same plan in a later phase.
/// </para>
/// </remarks>
public static class NetworkMatchSceneProjection
{
    /// <summary>Metres the orbit camera sits from the local tank (on a sphere around it).</summary>
    public const float ChaseDistanceMeters = 13f;

    /// <summary>Closest and farthest allowed chase-camera distances. The deliberately
    /// narrow range keeps the tank readable instead of turning the match into an overview.</summary>
    public const float MinChaseDistanceMeters = 7.5f;
    public const float MaxChaseDistanceMeters = 16f;

    /// <summary>Metres above the tank the camera aims at, so the hull sits low in frame.</summary>
    public const float TargetHeightMeters = 2f;

    /// <summary>Default orbit yaw (radians) before the player drags the camera — places it
    /// south of the tank looking north. RMB-drag rotates around this.</summary>
    public const float DefaultOrbitYawRadians = -MathF.PI / 2f;

    /// <summary>Default orbit pitch (radians above the horizon) — a gentle downward look.</summary>
    public const float DefaultOrbitPitchRadians = 0.42f;

    /// <summary>Vertical field of view for the match camera, in degrees.</summary>
    public const float FovYDegrees = 50f;

    /// <summary>Height of the fallback overview camera above the origin.</summary>
    public const float OverviewHeightMeters = 60f;

    /// <summary>How far south (−Z) of the origin the fallback overview camera sits, so it
    /// looks down the field at an angle rather than straight down.</summary>
    public const float OverviewBackMeters = 40f;

    /// <summary>Builds the scene plan for one frame. A null or empty snapshot yields the
    /// overview camera and no tanks.</summary>
    public static NetworkMatchScenePlan Build(
        WorldSnapshot? snapshot,
        uint localNetworkId,
        float orbitYawRadians = DefaultOrbitYawRadians,
        float orbitPitchRadians = DefaultOrbitPitchRadians,
        float chaseDistanceMeters = ChaseDistanceMeters,
        float? localBarrelPitchRadians = null,
        float localThrottle = 0f,
        float localSteering = 0f)
    {
        var felledProps = snapshot?.Props ?? (IReadOnlyList<PropSnapshot>)Array.Empty<PropSnapshot>();
        if (snapshot is null || snapshot.Entities.Count == 0)
        {
            return new NetworkMatchScenePlan(OverviewCamera(), Array.Empty<TankPlacement>())
            {
                LocalThrottle = localThrottle,
                LocalSteering = localSteering,
                DestroyedProps = felledProps,
            };
        }

        var tanks = new List<TankPlacement>(snapshot.Entities.Count);
        EntitySnapshot? self = null;
        foreach (var entity in snapshot.Entities)
        {
            var isSelf = localNetworkId != 0 && (uint)entity.Id == localNetworkId;
            if (isSelf)
            {
                self = entity;
            }

            var knockedOut = (entity.StateFlags & EntityStateFlags.KnockedOut) != EntityStateFlags.None;
            var barrelPitch = isSelf && localBarrelPitchRadians is { } localPitch
                ? ClampLocalBarrelPitch(localPitch, entity)
                : entity.BarrelPitchRadians;
            tanks.Add(new TankPlacement(
                Ground(entity.Position),
                entity.YawRadians,
                isSelf,
                knockedOut,
                entity.Id,
                entity.TurretYawRadians,
                barrelPitch,
                entity.GunRecoilTravelMeters));
        }

        var camera = self is { } me
            ? ChaseCamera(me, orbitYawRadians, orbitPitchRadians, chaseDistanceMeters)
            : OverviewCamera();
        return new NetworkMatchScenePlan(camera, tanks)
        {
            SnapshotTick = snapshot.Tick.Value,
            Projectiles = BuildProjectiles(snapshot),
            LocalThrottle = localThrottle,
            LocalSteering = localSteering,
            DestroyedProps = felledProps,
        };
    }

    private static ProjectilePlacement[] BuildProjectiles(WorldSnapshot snapshot)
    {
        var projectiles = new ProjectilePlacement[snapshot.Projectiles.Count];
        for (var i = 0; i < snapshot.Projectiles.Count; i++)
        {
            var projectile = snapshot.Projectiles[i];
            projectiles[i] = new ProjectilePlacement(
                new Vector3(projectile.Position.X, projectile.VisualHeightMeters, projectile.Position.Y),
                new Vector3(projectile.Velocity.X, projectile.VerticalVelocityMps, projectile.Velocity.Y),
                projectile.Id,
                new Vector3(
                    projectile.LaunchPosition.X,
                    projectile.LaunchVisualHeightMeters,
                    projectile.LaunchPosition.Y),
                projectile.OwnerEntityId);
        }

        return projectiles;
    }

    /// <summary>Orbit camera: sits on a sphere of <see cref="ChaseDistanceMeters"/> around
    /// the tank at the given yaw + pitch (driven by the player's RMB-drag, NOT the hull
    /// heading — so steering no longer swings the view) and looks back at the hull.</summary>
    private static CameraView3D ChaseCamera(
        EntitySnapshot self,
        float orbitYaw,
        float orbitPitch,
        float chaseDistanceMeters)
    {
        var groundPos = Ground(self.Position);
        var cosPitch = MathF.Cos(orbitPitch);
        var distance = float.IsFinite(chaseDistanceMeters)
            ? Math.Clamp(chaseDistanceMeters, MinChaseDistanceMeters, MaxChaseDistanceMeters)
            : ChaseDistanceMeters;
        var offset = new Vector3(
            MathF.Cos(orbitYaw) * cosPitch,
            MathF.Sin(orbitPitch),
            MathF.Sin(orbitYaw) * cosPitch) * distance;
        var target = groundPos + (Vector3.UnitY * TargetHeightMeters);
        return CameraView3D.LookAt(groundPos + offset, target, FovYDegrees);
    }

    private static CameraView3D OverviewCamera() =>
        CameraView3D.LookAt(new Vector3(0f, OverviewHeightMeters, -OverviewBackMeters), Vector3.Zero, FovYDegrees);

    private static float ClampLocalBarrelPitch(float pitchRadians, EntitySnapshot entity)
    {
        if (!float.IsFinite(pitchRadians))
        {
            return entity.BarrelPitchRadians;
        }

        if (!float.IsFinite(entity.MinBarrelPitchRadians)
            || !float.IsFinite(entity.MaxBarrelPitchRadians)
            || entity.MinBarrelPitchRadians > entity.MaxBarrelPitchRadians)
        {
            return pitchRadians;
        }

        return Math.Clamp(pitchRadians, entity.MinBarrelPitchRadians, entity.MaxBarrelPitchRadians);
    }

    /// <summary>World (X east, Y north) → 3D ground plane (X east, Z north), Y up.</summary>
    private static Vector3 Ground(Vector2 world) => new(world.X, 0f, world.Y);
}
