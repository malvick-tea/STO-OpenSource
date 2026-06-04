namespace Garupan.Sim.Components;

/// <summary>
/// Ballistics of the round currently chambered in a gun. Stamped from the catalogue at
/// spawn time (the spawner picks the first entry in the gun spec's supported_ammo list
/// and copies the matching AmmoSpec values across).
///
/// "Currently chambered" is one shell. The eventual ammo-rack model will let a player
/// switch ammunition mid-fight by rewriting these fields between shots; Phase 0 fires
/// the same round indefinitely.
///
/// Lethality is NOT a scalar on this struct. STO models tank knock-outs as a binary
/// penetration outcome (see <see cref="Projectile"/> + ProjectileHitResolveSystem),
/// so no damage table.
///
/// Ported from <c>svo/shared/components/gun.h</c>.
/// </summary>
public struct ChamberedRound
{
    public AmmoType Type;
    public float MuzzleVelocityMps;
    public float MassKg;
    public float DiameterMeters;
    public float DragCoefficient;
    public float PropellantChargeMassKg;
    public float GasVelocityFactor;

    /// <summary>Normal-incidence penetration table for the chambered round; the hit resolver
    /// samples it at the impact range and applies plate slope + obliquity geometrically.</summary>
    public PenetrationProfile Penetration;
}

/// <summary>
/// Per-gun ammunition rack. Phase 0 placeholder; runtime version holds rack capacity
/// + per-AmmoType counts + the chambered round.
/// </summary>
public struct AmmoLoadout
{
}

/// <summary>
/// Main armament. The gun is a separate component from the turret it sits in: a single
/// turret can host secondary weapons in principle, and decoupling firing (Gun) from
/// rotation (Turret) keeps per-system queries narrow.
///
/// Ported from <c>svo/shared/components/gun.h</c>.
/// </summary>
public struct Gun
{
    public CaliberId Caliber;
    public AmmoLoadout Ammo;
    public ChamberedRound Chambered;

    /// <summary>Authoritative reload time from the gun spec, seconds. Reset on fire.</summary>
    public float ReloadSecondsMax;

    /// <summary>Remaining reload time. Counts down each tick; gun fires when ≤ 0.</summary>
    public float ReloadSeconds;

    public float RecoilingAssemblyMassKg;
    public float MaximumRecoilTravelMeters;
    public float RecoilBrakeForceNewtons;
    public float MuzzleBrakeEfficiency;
    public float RecoilReturnSeconds;
}

/// <summary>Live recoil travel of the gun assembly. The discharge stamps the physical
/// peak travel and the fixed-step recoil system returns the assembly to battery.</summary>
public struct GunRecoilState
{
    public float TravelMeters;
    public float ReturnSpeedMetersPerSecond;
}
