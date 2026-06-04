using Garupan.Content;
using Garupan.Sim.Components;

namespace Garupan.Sim.Spawn;

/// <summary>
/// Produces hull-side components from a <see cref="TankSpec"/>: force-based ground
/// dynamics and the six-plate <see cref="ArmorMap"/>. Pure, with no ECS writes.
/// </summary>
public static class ChassisBuilder
{
    /// <summary>Fraction of the hull's total height at which the hull band gives way to the turret
    /// band. ~0.56 reproduces the legacy flat split for a medium tank (1.5 m / 2.68 m) while now scaling
    /// with each chassis's real <c>body_height</c> — a low casemate and a tall turret differ.</summary>
    private const float HullTurretSplitFraction = 0.56f;

    public static Hull Build(TankSpec spec) => new()
    {
        Type = TankId.None,
        Dynamics = GroundVehiclePhysicsFactory.Build(spec.Mobility),
        DynamicsState = Opus.Engine.Physics.Ground.GroundVehicleState.Rest(),
        Armor = BuildArmor(spec.Armor),
    };

    public static float HitRadiusMeters(TankSpec spec)
    {
        var halfLength = (float)spec.Mobility.BodyLengthMeters * 0.5f;
        var halfWidth = (float)spec.Mobility.BodyWidthMeters * 0.5f;
        return MathF.Sqrt((halfLength * halfLength) + (halfWidth * halfWidth));
    }

    public static Silhouette Silhouette(TankSpec spec)
    {
        var height = (float)spec.Mobility.BodyHeightMeters;
        return new Silhouette
        {
            HeightMeters = height,
            HullTurretSplitMeters = height * HullTurretSplitFraction,
        };
    }

    private static ArmorMap BuildArmor(ArmorProfile armor) => new()
    {
        HullFrontMm = armor.HullFront.ThicknessMm,
        HullFrontSlopeDeg = armor.HullFront.SlopeDegrees,
        HullSideMm = armor.HullSide.ThicknessMm,
        HullSideSlopeDeg = armor.HullSide.SlopeDegrees,
        HullRearMm = armor.HullRear.ThicknessMm,
        HullRearSlopeDeg = armor.HullRear.SlopeDegrees,
        TurretFrontMm = armor.TurretFront.ThicknessMm,
        TurretFrontSlopeDeg = armor.TurretFront.SlopeDegrees,
        TurretSideMm = armor.TurretSide.ThicknessMm,
        TurretSideSlopeDeg = armor.TurretSide.SlopeDegrees,
        TurretRearMm = armor.TurretRear.ThicknessMm,
        TurretRearSlopeDeg = armor.TurretRear.SlopeDegrees,
        MantletMm = armor.Mantlet.ThicknessMm,
        MantletSlopeDeg = armor.Mantlet.SlopeDegrees,
        RoofMm = armor.Roof.ThicknessMm,
        RoofSlopeDeg = armor.Roof.SlopeDegrees,
    };
}
