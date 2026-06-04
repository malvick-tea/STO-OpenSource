using System.Numerics;

namespace Garupan.Sim.Components;

/// <summary>
/// An impassable, immovable map obstacle as a simulation entity — a building footprint, a wall, a
/// bridge pier. Unlike <see cref="DestructibleProp"/> (light circular clutter a hull can fell) an
/// obstacle is a large oriented box that always blocks: no kinetic energy a tank can muster levels
/// it, so the collision response is purely "push the hull back out and slide along the face".
/// </summary>
/// <remarks>
/// The footprint is an oriented rectangle on the ground plane. Its centre lives on the entity's
/// <c>Transform.Position</c>; this component carries the half-extents plus the two world-space unit
/// axes the box is aligned to, precomputed from the spawn yaw (<see cref="Spawn.MapObstacleSpawner"/>)
/// so the per-tick broad-phase scan never recomputes trig. <see cref="LocalRight"/> spans
/// <see cref="HalfExtents"/>.X; <see cref="LocalForward"/> spans <see cref="HalfExtents"/>.Y.
/// </remarks>
public struct StaticObstacle
{
    /// <summary>Half-width (X, along <see cref="LocalRight"/>) and half-depth (Y, along
    /// <see cref="LocalForward"/>) of the footprint, in metres.</summary>
    public Vector2 HalfExtents;

    /// <summary>World-space unit axis the footprint's width is measured along — the box's local +X
    /// rotated into the world by the spawn yaw: <c>(cos θ, sin θ)</c>.</summary>
    public Vector2 LocalRight;

    /// <summary>World-space unit axis the footprint's depth is measured along — the box's local +Y
    /// rotated into the world by the spawn yaw: <c>(−sin θ, cos θ)</c>.</summary>
    public Vector2 LocalForward;
}
