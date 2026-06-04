using System;
using System.Numerics;

namespace Garupan.Sim.Terrain;

/// <summary>Seats a rigid rectangular hull on a height surface. The deck's tilt comes from a plane
/// fit through the four footprint corners (so a hull bridges sub-footprint terrain and leans only to
/// the real gradient, never to a point normal over a 0.18 m pole). On top of that the hull is lifted
/// until its underside clears the highest ground under either track — sampled densely along the two
/// track lines, finer than a felled pole is wide — so a tank <em>rides over</em> a fallen pole
/// instead of letting its tracks sink through it. A pole crossing under the belly between the corners
/// (which the four corners miss entirely) still lifts the hull; a pole that passes between the tracks
/// does not, exactly as a real chassis clears it.</summary>
public static class FootprintSurfaceFit
{
    /// <summary>Spacing of the track support samples. Finer than the thinnest felled prop a tank can
    /// climb (a lamp/utility pole is ~0.18 m across), so a pole lying across the hull is never stepped
    /// over between two samples.</summary>
    private const float StationSpacingMeters = 0.12f;

    /// <summary>Cap on samples per track so a very long footprint can't blow the per-frame budget; a
    /// 6 m hull needs ~50, well under this.</summary>
    private const int MaxStationsPerTrack = 96;

    /// <summary>The seated height and the upward plane normal for a footprint centred at world
    /// (x, z), yawed <paramref name="yawRadians"/> (sim convention: CCW from +X), with the given
    /// half-extents along its forward and right axes.</summary>
    public static (float Height, Vector3 Normal) At(
        IHeightSurface surface,
        float worldX,
        float worldZ,
        float yawRadians,
        float halfLengthMeters,
        float halfWidthMeters)
    {
        var forward = new Vector2(MathF.Cos(yawRadians), MathF.Sin(yawRadians));
        var right = new Vector2(-forward.Y, forward.X);
        var centre = new Vector2(worldX, worldZ);
        var front = forward * halfLengthMeters;
        var side = right * halfWidthMeters;

        var frontRight = Sample(surface, centre + front + side);
        var frontLeft = Sample(surface, centre + front - side);
        var rearRight = Sample(surface, centre - front + side);
        var rearLeft = Sample(surface, centre - front - side);

        var height = 0.25f * (frontRight + frontLeft + rearRight + rearLeft);

        // Tilt from the corner plane: bounded by how far a corner lifts over the half-extent it spans,
        // so the deck follows the true gradient and never the steep tangent of a tiny bump.
        var pitchSlope = ((frontRight + frontLeft) - (rearRight + rearLeft)) / (4f * halfLengthMeters);
        var rollSlope = ((frontRight + rearRight) - (frontLeft + rearLeft)) / (4f * halfWidthMeters);

        // Then ride over whatever that plane bridged: push the hull up until both tracks clear the
        // ground beneath them. Zero on smooth ground (the samples lie on the plane); a felled pole the
        // corners missed lifts the hull onto it.
        height += TrackClearanceLift(surface, centre, forward, side, halfLengthMeters, halfWidthMeters, height, pitchSlope, rollSlope);

        var gradient = (forward * pitchSlope) + (right * rollSlope);
        var normal = Vector3.Normalize(new Vector3(-gradient.X, 1f, -gradient.Y));
        return (height, normal);
    }

    /// <summary>How far to raise the seated plane so neither track sinks into the ground under it:
    /// the largest amount by which a sampled track point pokes above the tilted underside. Samples
    /// both track lines (centre ± the right half-width) along the full length at <see
    /// cref="StationSpacingMeters"/>.</summary>
    private static float TrackClearanceLift(
        IHeightSurface surface,
        Vector2 centre,
        Vector2 forward,
        Vector2 side,
        float halfLengthMeters,
        float halfWidthMeters,
        float height,
        float pitchSlope,
        float rollSlope)
    {
        var stations = StationCount(halfLengthMeters);
        var rollLift = rollSlope * halfWidthMeters;
        var lift = 0f;
        for (var i = 0; i < stations; i++)
        {
            var along = ((2f * i / (stations - 1)) - 1f) * halfLengthMeters;
            var stride = forward * along;
            var planeAtStation = height + (pitchSlope * along);
            var rightPoke = Sample(surface, centre + stride + side) - (planeAtStation + rollLift);
            var leftPoke = Sample(surface, centre + stride - side) - (planeAtStation - rollLift);
            lift = MathF.Max(lift, MathF.Max(rightPoke, leftPoke));
        }

        return lift;
    }

    /// <summary>Odd sample count for the track length, so one station sits dead-centre (catching a
    /// pole exactly under the hull's midline) and the spacing stays at or below the target.</summary>
    private static int StationCount(float halfLengthMeters)
    {
        var count = (int)MathF.Ceiling((2f * halfLengthMeters) / StationSpacingMeters) + 1;
        if ((count & 1) == 0)
        {
            count++;
        }

        return Math.Clamp(count, 3, MaxStationsPerTrack);
    }

    private static float Sample(IHeightSurface surface, Vector2 point) => surface.HeightAt(point.X, point.Y);
}
