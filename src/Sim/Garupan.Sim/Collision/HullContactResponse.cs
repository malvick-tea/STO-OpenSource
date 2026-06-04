using System.Numerics;
using Garupan.Sim.Components;

namespace Garupan.Sim.Collision;

/// <summary>
/// The shared "block" response when a hull cannot pass an obstacle it failed to defeat: push the
/// hull back out of the overlap and cancel the part of its velocity driving it into the obstacle, so
/// it stalls against the face instead of clipping through. Used by both the static-building collision
/// (<see cref="Systems.ObstacleCollisionSystem"/>) and the standing-prop collision
/// (<see cref="Systems.PropCollisionSystem"/>) so the stop behaviour is identical everywhere.
/// </summary>
public static class HullContactResponse
{
    /// <summary>Separates the hull from an obstacle along <paramref name="outwardDirection"/> (a
    /// world-space unit vector pointing from the obstacle toward the hull) by
    /// <paramref name="depthMeters"/>, then removes any inward component of the hull's velocity so it
    /// does not keep ploughing in next tick. The lateral (sliding) component survives untouched, so a
    /// tank scrubs along a wall rather than sticking to it.</summary>
    public static void Separate(ref Transform transform, ref Hull hull, Vector2 outwardDirection, float depthMeters)
    {
        transform.Position += outwardDirection * depthMeters;

        var velocity = hull.DynamicsState.VelocityMps;
        var outwardSpeed = Vector2.Dot(velocity, outwardDirection);
        if (outwardSpeed < 0f)
        {
            // Subtracting the (negative) inward projection zeroes the velocity component along the
            // contact normal, leaving only the slide-along-the-face component.
            hull.DynamicsState = hull.DynamicsState with { VelocityMps = velocity - (outwardDirection * outwardSpeed) };
        }
    }
}
