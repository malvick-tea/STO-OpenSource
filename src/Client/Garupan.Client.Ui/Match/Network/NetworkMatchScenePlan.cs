using System.Collections.Generic;
using System.Numerics;
using Garupan.Sim.Snapshot;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Match.Network;

/// <summary>
/// Placement of one tank in the 3D match scene: its world-space position (metres, on the
/// ground plane), hull yaw, and the lifecycle flags the renderer needs to tint it. Pure
/// data — produced by <see cref="NetworkMatchSceneProjection"/>, consumed by
/// <see cref="MatchSceneInstances"/> on the way to the scene renderer.
/// </summary>
public readonly record struct TankPlacement(
    Vector3 Position,
    float HullYawRadians,
    bool IsSelf,
    bool KnockedOut,
    int EntityId = 0,
    float TurretYawRadians = 0f,
    float BarrelPitchRadians = 0f,
    float GunRecoilTravelMeters = 0f);

/// <summary>An in-flight projectile drawn as a 3D shell: its world position (metres) and
/// world velocity (metres/second, full 3D), and immutable launch origin. The renderer
/// uses the origin for local muzzle VFX even when the first received snapshot arrives
/// after the shell has already moved downrange.</summary>
public readonly record struct ProjectilePlacement(
    Vector3 Position,
    Vector3 Velocity,
    int Id = 0,
    Vector3 LaunchPosition = default,
    int OwnerEntityId = 0);

/// <summary>
/// A frozen description of one frame of the 3D match scene: the camera to view it from,
/// and every tank to draw. Backend-agnostic — the camera is the engine's
/// <see cref="CameraView3D"/> and the tanks are plain placements — so the projection
/// maths stays headless-testable, separate from the <see cref="IMatchSceneRenderer"/>
/// that submits it to the GPU.
/// </summary>
public sealed record NetworkMatchScenePlan(CameraView3D Camera, IReadOnlyList<TankPlacement> Tanks)
{
    /// <summary>Authoritative snapshot tick. Render backends use it to integrate visual
    /// motion once even if the same plan is submitted for several display frames.</summary>
    public long SnapshotTick { get; init; }

    /// <summary>In-flight projectiles drawn as 3D shells — world position + velocity per
    /// round. Init-only with an empty default so tanks-only construction stays a
    /// two-argument call.</summary>
    public IReadOnlyList<ProjectilePlacement> Projectiles { get; init; } = System.Array.Empty<ProjectilePlacement>();

    /// <summary>Props the server reports no longer standing this frame — felled poles, toppling
    /// signs, shattered clutter — keyed by stable prop id. The renderer owns the static prop layout
    /// and overrides only these to their broken pose, so the player sees a smashed pole break exactly
    /// as the sim resolved it. Empty for replays and any frame before the first felling.</summary>
    public IReadOnlyList<PropSnapshot> DestroyedProps { get; init; } = System.Array.Empty<PropSnapshot>();

    /// <summary>The local player's signed drive command this frame (+forward / −reverse,
    /// magnitude ≤ 1). It is the player's own throttle, not a snapshot-derived value, so the
    /// self tank's engine and track audio respond to drive effort even when the hull is not
    /// translating — pinned against an unclimbable slope, say. Zero for replays and any
    /// frame with no local input.</summary>
    public float LocalThrottle { get; init; }

    /// <summary>The local player's signed steering command this frame (−left / +right,
    /// magnitude ≤ 1). Like <see cref="LocalThrottle"/> it is the player's own input, so a
    /// stationary pivot turn (steering with no throttle — the tracks counter-rotate and the
    /// hull spins in place without translating) still drives the engine and track audio.
    /// Zero for replays and any frame with no local input.</summary>
    public float LocalSteering { get; init; }
}
