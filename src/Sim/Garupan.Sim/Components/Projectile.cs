using System.Numerics;
using Opus.Engine.Physics.Ballistics;

namespace Garupan.Sim.Components;

/// <summary>
/// In-flight round paired with a <see cref="Transform"/>. Horizontal pose stays in the
/// existing top-down ECS plane while height, vertical speed, retained aerodynamic
/// properties, elapsed flight time, and travelled distance preserve full exterior
/// ballistics. Previous pose fields feed swept collision between fixed ticks.
/// </summary>
public struct Projectile
{
    public Vector2 PreviousPosition;
    public Vector2 Velocity;
    public float PreviousVisualHeightMeters;
    public float VisualHeightMeters;
    public Vector2 LaunchPosition;
    public float LaunchVisualHeightMeters;
    public float VerticalVelocityMps;
    public float MuzzleVelocityMps;
    public float FlightSeconds;
    public float DistanceTravelledMeters;
    public bool HasIntegratedSegment;
    public bool HitGround;
    public BallisticBodyProperties? Dynamics;
    public float MassKg;
    public AmmoType Type;

    /// <summary>Normal-incidence penetration table carried from the chambered round; sampled at
    /// the impact range by <see cref="Systems.ProjectileHitResolveSystem"/>.</summary>
    public PenetrationProfile Penetration;
}
