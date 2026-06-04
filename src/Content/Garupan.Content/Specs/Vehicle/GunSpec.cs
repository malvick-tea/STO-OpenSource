namespace Garupan.Content;

/// <summary>
/// Main gun ballistics + rate-of-fire envelope. The <see cref="DefaultAmmoId"/> field
/// resolves through <see cref="AmmoCatalog.FindById"/> at spawn time to populate the
/// chambered round — when the rack/loadout system lands (M5+) this becomes the first
/// entry in a list of supported families.
///
/// <see cref="PenetrationMm"/> is retained as a quick-glance figure for tooling / HUD
/// but the simulation reads penetration from the chambered round, not from this scalar.
/// </summary>
public sealed record GunSpec(
    string Id,
    string Caliber,
    int PenetrationMm,
    int Damage,
    double ReloadSeconds,
    int RoundsPerMinute,
    string DefaultAmmoId,
    double RecoilingAssemblyMassKg,
    double MaximumRecoilTravelMeters,
    double RecoilBrakeForceNewtons,
    double MuzzleBrakeEfficiency,
    double RecoilReturnSeconds);
