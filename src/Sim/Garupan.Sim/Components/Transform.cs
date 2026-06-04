using System.Numerics;

namespace Garupan.Sim.Components;

/// <summary>
/// World-space pose of an entity in the top-down 2D simulation plane. Z axis is implicit
/// — Phase 0 collapses elevation into the 2D plane and does not model arc trajectories.
/// Heading is stored as a scalar yaw rather than a quaternion because the world is flat
/// and yaw is the only rotational degree of freedom at the hull level; turret yaw is
/// layered on top via <see cref="Turret"/>.
///
/// Units:
/// <list type="bullet">
/// <item><description>Position — metres, world frame, +X east, +Y north.</description></item>
/// <item><description>YawRadians — radians, counter-clockwise positive, 0 = facing +X.</description></item>
/// </list>
/// Ported from <c>svo/shared/components/transform.h</c>.
/// </summary>
public struct Transform
{
    public Vector2 Position;
    public float YawRadians;

    public Transform(Vector2 position, float yawRadians)
    {
        Position = position;
        YawRadians = yawRadians;
    }
}
