using System.Numerics;

namespace Garupan.Garage.Demo;

/// <summary>Tuning parameters for <see cref="CasingEjector"/>. Constructed once per
/// ejector instance — typical hosts use <see cref="Default"/>, while tests pass tighter
/// values (e.g. larger assignment radius for compact fixtures) to verify behaviour.</summary>
/// <param name="EjectionRearOffsetMeters">How far behind the tank centre the casing
/// spawns along the tank's rear axis. ~1.2m clears the rear hull plate of a medium tank.</param>
/// <param name="EjectionHeightMeters">Spawn altitude above ground. ~1.7m matches the
/// turret roof of a medium tank, where the ejection port would sit on a real heavy gun.</param>
/// <param name="EjectionRearSpeedMps">Initial horizontal velocity, applied along the
/// rear vector. 2 m/s reads as "tossed back" without flinging across the map.</param>
/// <param name="EjectionUpwardSpeedMps">Initial vertical velocity, applied along +Y.
/// Combined with gravity, gives a small arc before the casing lands.</param>
/// <param name="ShooterAssignmentRadiusMeters">Maximum tank-to-projectile distance for
/// a tank to be considered the shooter. ~5m covers spawn-jitter; projectiles that
/// emerge from further away (no plausible shooter nearby) get no casing.</param>
/// <param name="LifetimeSeconds">How long each casing renders before it despawns.
/// 3 seconds is enough for the visual arc to read but doesn't litter the map.</param>
/// <param name="GravityMps2">Gravity vector applied per tick to each casing velocity.
/// World-space, default <c>(0, -9.81, 0)</c>.</param>
/// <param name="TumbleAngularVelocityRadPerSec">Constant angular velocity applied to
/// every casing's rotation. Deterministic (no per-casing randomness) so tests can
/// reproduce exact rotations; visual variety could be added later via id-derived
/// jitter.</param>
public sealed record CasingEjectorConfig(
    float EjectionRearOffsetMeters,
    float EjectionHeightMeters,
    float EjectionRearSpeedMps,
    float EjectionUpwardSpeedMps,
    float ShooterAssignmentRadiusMeters,
    float LifetimeSeconds,
    Vector3 GravityMps2,
    Vector3 TumbleAngularVelocityRadPerSec)
{
    public static CasingEjectorConfig Default { get; } = new(
        EjectionRearOffsetMeters: 1.2f,
        EjectionHeightMeters: 1.7f,
        EjectionRearSpeedMps: 2.0f,
        EjectionUpwardSpeedMps: 1.5f,
        ShooterAssignmentRadiusMeters: 5.0f,
        LifetimeSeconds: 3.0f,
        GravityMps2: new Vector3(0f, -9.81f, 0f),
        TumbleAngularVelocityRadPerSec: new Vector3(5f, 3f, 7f));
}
