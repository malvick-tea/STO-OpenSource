using System;
using System.Numerics;

namespace Garupan.Sim.Systems;

/// <summary>
/// Pure armour-penetration geometry: turns a plate's mounting slope and a shot's azimuth into an
/// impact obliquity, then into the effective line-of-sight thickness a perpendicular round must
/// defeat. Kept separate from <see cref="ProjectileHitResolveSystem"/> so the geometry is unit
/// testable against published oblique-penetration figures without standing up an ECS world.
/// </summary>
public static class ArmorPenetrationResolver
{
    private const float DegreesToRadians = MathF.PI / 180f;
    private const float RadiansToDegrees = 180f / MathF.PI;
    private const float MinimumHeading = 1e-6f;

    /// <summary>
    /// Cosine floor for the line-of-sight division: a near-grazing hit would otherwise demand an
    /// unbounded thickness. Caps the geometric benefit at ~78.5° obliquity, past which ricochet —
    /// not raw line-of-sight — dominates and is left to a future shatter/ricochet model.
    /// </summary>
    private const float MinimumObliquityCosine = 0.20f;

    /// <summary>
    /// Impact obliquity from the struck plate's outward normal, in degrees — the compound of the
    /// plate's vertical mounting slope and the shot's horizontal azimuth off that normal. A round
    /// with no horizontal heading (a hand-built test round) is treated as striking head-on, so the
    /// obliquity reduces to the plate slope alone.
    /// </summary>
    public static float ObliquityDegrees(float plateSlopeDegrees, Vector2 shotVelocity, Vector2 plateOutwardNormal)
    {
        var azimuthCosine = 1f;
        var speedSquared = shotVelocity.LengthSquared();
        if (speedSquared > MinimumHeading)
        {
            var heading = shotVelocity / MathF.Sqrt(speedSquared);

            // The shot travels into the plate, roughly opposite the outward normal; a perpendicular
            // strike has heading == -normal, giving cosine 1 (zero obliquity).
            azimuthCosine = Math.Clamp(-Vector2.Dot(heading, plateOutwardNormal), 0f, 1f);
        }

        var slopeCosine = MathF.Cos(plateSlopeDegrees * DegreesToRadians);
        var compoundCosine = Math.Clamp(slopeCosine * azimuthCosine, -1f, 1f);
        return MathF.Acos(compoundCosine) * RadiansToDegrees;
    }

    /// <summary>
    /// Effective line-of-sight thickness, in millimetres, a perpendicular round must defeat to pass
    /// a plate of <paramref name="thicknessMm"/> struck at <paramref name="obliquityDegrees"/>.
    /// </summary>
    public static float EffectiveThicknessMm(float thicknessMm, float obliquityDegrees)
    {
        var cosine = MathF.Cos(obliquityDegrees * DegreesToRadians);
        return thicknessMm / MathF.Max(cosine, MinimumObliquityCosine);
    }
}
