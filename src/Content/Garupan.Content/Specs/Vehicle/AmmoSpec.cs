namespace Garupan.Content;

/// <summary>
/// Ballistic data for one round in the ammunition catalogue. The four <see cref="AmmoType"/>
/// variants each resolve to one or more <see cref="AmmoSpec"/> rows — different
/// AP loads for different guns share the same family but carry different muzzle
/// velocity / penetration figures.
///
/// No damage scalar: STO models knock-outs as a binary penetration outcome (see
/// <c>Garupan.Sim.Systems.ProjectileHitResolveSystem</c>), so the resolver compares
/// <see cref="PenetrationMm"/> against the target plate and tags <c>KnockedOut</c> on
/// success. Falloff curves and angle-of-attack are applied downstream.
///
/// Ported from <c>svo::shared::data::AmmoSpec</c>.
/// </summary>
public sealed record AmmoSpec(
    string Id,
    AmmoType Type,
    float MuzzleVelocityMps,
    float MassKg,
    float PenetrationMm,
    float DiameterMeters,
    float DragCoefficient,
    float PropellantChargeMassKg,
    float GasVelocityFactor);
