using Garupan.Content;
using Opus.Engine.Physics;
using Opus.Engine.Physics.Ground;

namespace Garupan.Sim.Spawn;

/// <summary>
/// Garupan catalogue adapter for the game-agnostic Opus ground-vehicle model. All
/// tracked-vehicle calibration lives in <see cref="MobilitySpec"/>; the Opus solver only
/// consumes neutral force-model inputs.
/// </summary>
public static class GroundVehiclePhysicsFactory
{
    /// <summary>Track half-gauge as a fraction of hull width — the lever arm the skid-steer
    /// torque acts through. ~0.4 puts the track centreline a little inboard of the hull edge,
    /// matching the wartime tracked layouts the roster models.</summary>
    private const float TrackHalfGaugeWidthFraction = 0.4f;

    /// <summary>Terrain slope is sampled over a fraction of the hull length (so a longer tank
    /// averages a longer span), floored to a minimum so a short chassis still samples a
    /// meaningful baseline rather than a near-point gradient.</summary>
    private const float SlopeSampleHullLengthFraction = 0.35f;
    private const float MinimumSlopeSampleDistanceMeters = 0.5f;

    public static GroundVehicleProperties Build(MobilitySpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var massKg = (float)spec.MassTonnes * 1000f;
        var steeringTorque = SteeringTorque(spec, massKg);
        var drive = spec.Drive;
        var powertrain = new PowertrainProperties(
            TorqueCurve.BroadPeak(
                (float)drive.TorqueIdleRpm,
                (float)drive.TorquePeakRpm,
                (float)drive.TorqueRedlineRpm,
                (float)spec.EnginePeakTorqueNewtonMeters),
            ToFloatArray(drive.ForwardGearRatios),
            reverseGearRatio: (float)drive.ReverseGearRatio,
            finalDriveRatio: (float)drive.FinalDriveRatio,
            drivenRadiusMeters: (float)spec.DrivenRadiusMeters,
            efficiency: (float)spec.DrivetrainEfficiency,
            idleRpm: (float)drive.IdleRpm,
            maximumPowerWatts: (float)spec.EnginePowerHorsepower * PhysicsConstants.HorsepowerToWatts,
            upshiftRpm: (float)drive.UpshiftRpm,
            downshiftRpm: (float)drive.DownshiftRpm);
        return new GroundVehicleProperties(
            massKg,
            frontalAreaSquareMeters: (float)(spec.BodyWidthMeters * spec.BodyHeightMeters),
            aerodynamicDragCoefficient: (float)spec.AerodynamicDragCoefficient,
            rollingResistanceCoefficient: (float)spec.RollingResistanceCoefficient,
            tractionScale: (float)spec.TractionScale,
            lateralGripScale: (float)spec.LateralGripScale,
            maximumBrakeForceNewtons: massKg * (float)spec.MaximumBrakeDecelerationMps2,
            yawInertiaKgSquareMeters: YawInertia(spec, massKg),
            maximumSteeringTorqueNewtonMeters: steeringTorque,
            angularDampingNewtonMeterSeconds: steeringTorque / (float)drive.MaximumHullTraverseRadiansPerSecond,
            powertrain,
            engineBrakingCoefficientNsPerM: massKg * (float)drive.EngineBrakingRatePerSecond,
            turningResistanceCoefficientSeconds: (float)drive.TurningResistanceCoefficientSeconds,
            terrainSlopeSampleDistanceMeters: TerrainSlopeSampleDistance(spec));
    }

    private static float YawInertia(MobilitySpec spec, float massKg)
    {
        var length = (float)spec.BodyLengthMeters;
        var width = (float)spec.BodyWidthMeters;
        return massKg * ((length * length) + (width * width)) / 12f;
    }

    private static float SteeringTorque(MobilitySpec spec, float massKg)
    {
        var halfGauge = (float)spec.BodyWidthMeters * TrackHalfGaugeWidthFraction;
        return massKg * PhysicsConstants.StandardGravityMps2 * halfGauge * (float)spec.SteeringTorqueScale;
    }

    private static float TerrainSlopeSampleDistance(MobilitySpec spec) =>
        MathF.Max(MinimumSlopeSampleDistanceMeters, (float)spec.BodyLengthMeters * SlopeSampleHullLengthFraction);

    private static float[] ToFloatArray(System.Collections.Generic.IReadOnlyList<double> source)
    {
        var values = new float[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            values[i] = (float)source[i];
        }

        return values;
    }
}
