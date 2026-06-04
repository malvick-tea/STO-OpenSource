namespace Garupan.Content;

/// <summary>
/// One armour plate: its physical thickness and its mounting slope, in degrees from vertical
/// (0° = a vertical plate, 60° = the medium tank E glacis, 90° = a horizontal roof). The simulation
/// resolves effective line-of-sight thickness from the slope and the shot's obliquity, so a
/// thin sloped plate (45 mm @ 60°) defends like a far thicker vertical one.
/// </summary>
public sealed record ArmorPlate(int ThicknessMm, int SlopeDegrees);

/// <summary>
/// Layered plate thicknesses + mounting slopes for one vehicle. Hull and turret are tracked
/// independently — a hit landing high on the silhouette resolves against the turret — alongside
/// the gun mantlet and the roof. This replaces the earlier single front/side/rear/roof scalar
/// set, which could not represent a tank whose turret and hull are armoured differently
/// (medium tank E, MediumF, MediumC, heavy tank B, medium tank D) nor the slope that makes thin sloped plate
/// effective. Casemate vehicles (AssaultGun III) repeat the fighting-compartment front in the turret
/// fields so a high hit still resolves correctly.
/// </summary>
public sealed record ArmorProfile(
    ArmorPlate HullFront,
    ArmorPlate HullSide,
    ArmorPlate HullRear,
    ArmorPlate TurretFront,
    ArmorPlate TurretSide,
    ArmorPlate TurretRear,
    ArmorPlate Mantlet,
    ArmorPlate Roof);
