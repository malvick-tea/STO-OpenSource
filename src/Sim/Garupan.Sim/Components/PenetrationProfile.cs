namespace Garupan.Sim.Components;

/// <summary>
/// Normal-incidence (0° obliquity) penetration of a chambered round, in millimetres, at three
/// reference ranges (100 / 500 / 1000 m). Baked onto the round at spawn from the ammunition
/// catalogue; <see cref="Systems.ProjectileHitResolveSystem"/> samples it at the impact range
/// (piecewise-linear, clamped) and then applies the struck plate's slope and the shot's azimuth
/// geometrically. Replaces the earlier single scalar plus velocity-squared falloff: distance
/// falloff now comes from the published range table, obliquity from the plate geometry.
/// </summary>
public struct PenetrationProfile
{
    private const float NearRangeMeters = 100f;
    private const float MidRangeMeters = 500f;
    private const float FarRangeMeters = 1000f;

    public float Normal100Mm;
    public float Normal500Mm;
    public float Normal1000Mm;

    /// <summary>A range-independent profile — identical penetration at every distance. Convenience
    /// for shaped charges (flat versus range) and for hand-built test rounds that do not exercise
    /// distance falloff.</summary>
    public static PenetrationProfile Flat(float millimetres) => new()
    {
        Normal100Mm = millimetres,
        Normal500Mm = millimetres,
        Normal1000Mm = millimetres,
    };

    /// <summary>Normal-incidence penetration at <paramref name="rangeMeters"/>, piecewise-linear
    /// between the 100 / 500 / 1000 m samples and held flat outside that band.</summary>
    public readonly float NormalPenetrationAt(float rangeMeters)
    {
        if (rangeMeters <= NearRangeMeters)
        {
            return Normal100Mm;
        }

        if (rangeMeters >= FarRangeMeters)
        {
            return Normal1000Mm;
        }

        return rangeMeters <= MidRangeMeters
            ? Lerp(Normal100Mm, Normal500Mm, (rangeMeters - NearRangeMeters) / (MidRangeMeters - NearRangeMeters))
            : Lerp(Normal500Mm, Normal1000Mm, (rangeMeters - MidRangeMeters) / (FarRangeMeters - MidRangeMeters));
    }

    private static float Lerp(float from, float to, float t) => from + ((to - from) * t);
}
