using Garupan.Content;
using Garupan.Sim.Components;
using SimAmmo = Garupan.Sim.Components.AmmoType;

namespace Garupan.Sim.Spawn;

/// <summary>
/// Produces the <see cref="Turret"/> and <see cref="Gun"/> components for a spawn —
/// including the chambered round resolved through <see cref="AmmoCatalog"/>. Pure.
///
/// Reload primes to max so the first shot pays the same cooldown as later shots: an
/// opening volley firing the instant the entity exists would skew the early-match
/// balance and isn't physically plausible.
/// </summary>
public static class TurretGunBuilder
{
    public static Turret BuildTurret(TankSpec spec, float yawRadians = 0f) => new()
    {
        YawRadians = yawRadians,
        TraverseSpeedRadPerS = SpecConversion.DegPerSecToRadPerSec(spec.Mobility.TurretTraverseDegPerSec),
    };

    public static Gun BuildGun(GunSpec gun)
    {
        var reload = (float)gun.ReloadSeconds;
        return new Gun
        {
            Caliber = CaliberId.None,
            ReloadSecondsMax = reload,
            ReloadSeconds = reload,
            Chambered = ResolveChamberedRound(gun),
            RecoilingAssemblyMassKg = (float)gun.RecoilingAssemblyMassKg,
            MaximumRecoilTravelMeters = (float)gun.MaximumRecoilTravelMeters,
            RecoilBrakeForceNewtons = (float)gun.RecoilBrakeForceNewtons,
            MuzzleBrakeEfficiency = (float)gun.MuzzleBrakeEfficiency,
            RecoilReturnSeconds = (float)gun.RecoilReturnSeconds,
        };
    }

    public static GunMount BuildGunMount(GunMountSpec mount) => new()
    {
        MinPitchRadians = (float)mount.MinPitchDegrees * SpecConversion.DegreesToRadians,
        MaxPitchRadians = (float)mount.MaxPitchDegrees * SpecConversion.DegreesToRadians,
        TrunnionForwardMeters = (float)mount.TrunnionForwardMeters,
        TrunnionHeightMeters = (float)mount.TrunnionHeightMeters,
        BarrelLengthMeters = (float)mount.BarrelLengthMeters,
    };

    /// <summary>
    /// Looks up the gun's default round in <see cref="AmmoCatalog"/>. Catalogue validation
    /// should make a miss unreachable in runtime; failing loudly here keeps malformed
    /// authoring data from silently borrowing another weapon's physical envelope.
    /// </summary>
    private static ChamberedRound ResolveChamberedRound(GunSpec gun)
    {
        var ammo = AmmoCatalog.FindById(gun.DefaultAmmoId)
            ?? throw new InvalidOperationException(
                $"Gun \"{gun.Id}\" references unknown default ammo id \"{gun.DefaultAmmoId}\".");

        // The round's defeat ability is its published normal-incidence range table, not a single
        // scalar — the resolver applies slope/obliquity. A missing table is malformed authoring.
        var curve = AmmoPenetrationCatalog.RequireById(gun.DefaultAmmoId);

        return new ChamberedRound
        {
            // Sim and Content mirror AmmoType numerically; a plain cast round-trips
            // (asserted by a test so future divergence is loud, not silent).
            Type = (SimAmmo)(byte)ammo.Type,
            MuzzleVelocityMps = ammo.MuzzleVelocityMps,
            MassKg = ammo.MassKg,
            DiameterMeters = ammo.DiameterMeters,
            DragCoefficient = ammo.DragCoefficient,
            PropellantChargeMassKg = ammo.PropellantChargeMassKg,
            GasVelocityFactor = ammo.GasVelocityFactor,
            Penetration = new PenetrationProfile
            {
                Normal100Mm = curve.Normal100Mm,
                Normal500Mm = curve.Normal500Mm,
                Normal1000Mm = curve.Normal1000Mm,
            },
        };
    }
}
