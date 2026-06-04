namespace Garupan.Sim.Spawn;

/// <summary>Catalogue ergonomics to simulation SI conversions.</summary>
public static class SpecConversion
{
    public const float DegreesToRadians = MathF.PI / 180f;

    public static float DegPerSecToRadPerSec(int degPerSec) => degPerSec * DegreesToRadians;
}
