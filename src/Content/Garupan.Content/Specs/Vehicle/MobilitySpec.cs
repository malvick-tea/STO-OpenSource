namespace Garupan.Content;

/// <summary>
/// Physical chassis and powertrain inputs. Maximum speed and hull traverse are
/// intentionally absent: the simulation derives motion from forces through the
/// game-agnostic Opus physics module.
/// </summary>
public sealed record MobilitySpec(
    double MassTonnes,
    double EnginePowerHorsepower,
    double EnginePeakTorqueNewtonMeters,
    double BodyLengthMeters,
    double BodyWidthMeters,
    double BodyHeightMeters,
    int TurretTraverseDegPerSec,
    GroundDriveSpec Drive,
    double DrivenRadiusMeters = 0.35,
    // Tracks on firm ground sit at ~0.05–0.09. This 0.06 is only the fallback for callers that
    // build a spec directly; the roster sets rolling_resistance PER-TANK in data/tanks.csv,
    // calibrated so each chassis hits its historical road speed on the compacted-earth surface
    // (lighter coefficients for the faster HeavyA/MediumC/medium tank E/MediumF, heavier for the ponderous
    // heavy tank D). The resulting power-limited terminal speeds are locked by RosterTopSpeedTests.
    double RollingResistanceCoefficient = 0.06,
    double AerodynamicDragCoefficient = 0.9,
    double TractionScale = 0.95,
    double LateralGripScale = 0.9,
    double DrivetrainEfficiency = 0.72,
    double MaximumBrakeDecelerationMps2 = 4.0,
    double SteeringTorqueScale = 0.85);
