using System.Numerics;

namespace Garupan.Sim.Components;

/// <summary>Live gun-installation geometry copied from the chassis catalogue at spawn.
/// Every placement and pitch clamp is instance data; the simulation has no model-specific
/// barrel constants.</summary>
public struct GunMount
{
    public float MinPitchRadians;
    public float MaxPitchRadians;
    public float TrunnionForwardMeters;
    public float TrunnionHeightMeters;
    public float BarrelLengthMeters;

    public readonly Vector2 MuzzlePosition(Vector2 tankPosition, float turretYawRadians, float barrelPitchRadians)
    {
        var forwardMeters = ForwardMeters(barrelPitchRadians);
        return tankPosition + new Vector2(
            MathF.Cos(turretYawRadians) * forwardMeters,
            MathF.Sin(turretYawRadians) * forwardMeters);
    }

    public readonly float ForwardMeters(float barrelPitchRadians) =>
        TrunnionForwardMeters + (MathF.Cos(barrelPitchRadians) * BarrelLengthMeters);

    public readonly float MuzzleHeightMeters(float barrelPitchRadians) =>
        TrunnionHeightMeters + (MathF.Sin(barrelPitchRadians) * BarrelLengthMeters);
}
