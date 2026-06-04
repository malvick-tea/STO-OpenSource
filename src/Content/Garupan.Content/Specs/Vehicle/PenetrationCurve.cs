namespace Garupan.Content;

/// <summary>
/// Normal-incidence (0° obliquity) penetration of homogeneous rolled armour for one round, in
/// millimetres, sampled at 100 / 500 / 1000 m. Distance falloff lives here; obliquity does not —
/// the simulation derives oblique performance geometrically from the struck plate's slope and the
/// shot's azimuth, so these are the perpendicular figures. This replaces the single scalar +
/// velocity-squared falloff the resolver used previously: a round's defeat ability against a
/// sloped plate is now the published range table read against the geometric line-of-sight.
/// </summary>
public sealed record PenetrationCurve(
    string AmmoId,
    float Normal100Mm,
    float Normal500Mm,
    float Normal1000Mm);
