using System;
using System.Numerics;
using Garupan.Sim.Components;

namespace Garupan.Sim.Terrain;

/// <summary>One felled prop lying over the ground: where it is rooted, the heading it toppled toward,
/// its length and radius, and its lifecycle state. A toppling or fallen member is a low cylinder a
/// tank can ride over; a standing or shattered one contributes no drive-over height.</summary>
public readonly record struct FelledPropSurfaceMember(
    Vector2 BasePosition,
    float FallYawRadians,
    float LengthMeters,
    float RadiusMeters,
    PropState State);

/// <summary>Drive-over height of a toppled cylindrical prop lying on the ground — the small bump a
/// tank's track rises over. Pure geometry: a member is a horizontal cylinder of <c>RadiusMeters</c>
/// running <c>LengthMeters</c> from its base along its fall heading, so the surface it adds is the
/// top of that cylinder (peaking at one diameter above ground over the axis, fading to nothing at
/// the rim).</summary>
public static class FelledPropSurface
{
    private const float MinimumDimensionMeters = 1e-4f;

    /// <summary>A prop adds drive-over height only once it is hinging over or down — a standing prop
    /// is still an obstacle (handled by <see cref="Systems.PropCollisionSystem"/>), a shattered one
    /// is cleared away.</summary>
    public static bool IsContactable(PropState state) =>
        state is PropState.Toppling or PropState.Fallen;

    /// <summary>Height the member adds at world (x, z): the top of the lying cylinder where the point
    /// is within its radius of the felled axis, else zero.</summary>
    public static float HeightContribution(in FelledPropSurfaceMember member, float worldX, float worldZ)
    {
        if (!IsContactable(member.State)
            || member.LengthMeters <= MinimumDimensionMeters
            || member.RadiusMeters <= MinimumDimensionMeters)
        {
            return 0f;
        }

        var distance = DistanceToFelledAxis(member, new Vector2(worldX, worldZ));
        if (distance >= member.RadiusMeters)
        {
            return 0f;
        }

        // Top of a horizontal cylinder resting on the ground: its axis sits one radius up, so the
        // surface peaks at one diameter over the axis and falls to the radius height at the rim.
        return member.RadiusMeters
            + MathF.Sqrt((member.RadiusMeters * member.RadiusMeters) - (distance * distance));
    }

    /// <summary>Perpendicular distance from <paramref name="point"/> to the felled member's axis
    /// segment (base → base + heading·length), clamped to the segment ends.</summary>
    private static float DistanceToFelledAxis(in FelledPropSurfaceMember member, Vector2 point)
    {
        var heading = new Vector2(MathF.Cos(member.FallYawRadians), MathF.Sin(member.FallYawRadians));
        var axis = heading * member.LengthMeters;
        var t = Math.Clamp(Vector2.Dot(point - member.BasePosition, axis) / axis.LengthSquared(), 0f, 1f);
        return Vector2.Distance(point, member.BasePosition + (axis * t));
    }
}
