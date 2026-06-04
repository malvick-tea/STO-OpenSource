using System;
using System.Numerics;

namespace Garupan.Sim.Collision;

/// <summary>Result of testing a circle against an oriented box: whether they overlap and, if so,
/// the world-space unit direction to move the circle along — and the distance to move it — to just
/// clear the overlap. <see cref="PushDirection"/> points from the box toward the circle.</summary>
public readonly record struct CircleBoxContact(bool Overlaps, Vector2 PushDirection, float Depth)
{
    public static readonly CircleBoxContact None = new(false, Vector2.Zero, 0f);
}

/// <summary>
/// Separates a moving circle (a tank's planar hit volume) from a static oriented box (a building
/// footprint). Pure planar geometry — no ECS, no engine types — so it is fully unit-testable and is
/// shared by every obstacle in the world regardless of tank or building, with no per-pair tuning.
/// </summary>
/// <remarks>
/// The circle centre is projected into the box's local frame (its right / forward unit axes). The
/// closest point on the box to the centre is the per-axis clamp of that projection to the box's
/// half-extents. When the centre lies outside the box, the overlap is along the vector from that
/// closest point to the centre. When the centre lies inside the footprint, the box is exited along
/// the axis of least penetration — the shortest push that frees the hull, so a tank that has nosed
/// into a corner slides out the nearer face instead of jumping across the building.
/// </remarks>
public static class CircleBoxCollision
{
    public static CircleBoxContact Resolve(
        Vector2 circleCenter,
        float circleRadius,
        Vector2 boxCenter,
        Vector2 boxRight,
        Vector2 boxForward,
        Vector2 boxHalfExtents)
    {
        var offset = circleCenter - boxCenter;
        var localX = Vector2.Dot(offset, boxRight);
        var localY = Vector2.Dot(offset, boxForward);
        var clampedX = Math.Clamp(localX, -boxHalfExtents.X, boxHalfExtents.X);
        var clampedY = Math.Clamp(localY, -boxHalfExtents.Y, boxHalfExtents.Y);

        // Inside the footprint when the clamp moved nothing — exit along the least-penetration face.
        if (localX == clampedX && localY == clampedY)
        {
            return ResolveFromInside(localX, localY, circleRadius, boxRight, boxForward, boxHalfExtents);
        }

        var deltaX = localX - clampedX;
        var deltaY = localY - clampedY;
        var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
        if (distanceSquared >= circleRadius * circleRadius)
        {
            return CircleBoxContact.None;
        }

        var distance = MathF.Sqrt(distanceSquared);
        var pushDirection = ((deltaX * boxRight) + (deltaY * boxForward)) / distance;
        return new CircleBoxContact(true, pushDirection, circleRadius - distance);
    }

    private static CircleBoxContact ResolveFromInside(
        float localX,
        float localY,
        float circleRadius,
        Vector2 boxRight,
        Vector2 boxForward,
        Vector2 boxHalfExtents)
    {
        var penetrationX = boxHalfExtents.X - MathF.Abs(localX);
        var penetrationY = boxHalfExtents.Y - MathF.Abs(localY);
        if (penetrationX <= penetrationY)
        {
            var sign = localX >= 0f ? 1f : -1f;
            return new CircleBoxContact(true, sign * boxRight, penetrationX + circleRadius);
        }

        var signY = localY >= 0f ? 1f : -1f;
        return new CircleBoxContact(true, signY * boxForward, penetrationY + circleRadius);
    }
}
