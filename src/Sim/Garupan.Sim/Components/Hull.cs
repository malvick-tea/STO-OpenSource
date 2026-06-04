namespace Garupan.Sim.Components;

/// <summary>
/// Hull of a tank — the chassis identifier plus the hit/damage state that lives at the
/// chassis level (armour plates and internal modules) and retained physical dynamics
/// state. One of two top-level "vehicle" components; the
/// second is <see cref="Turret"/>, intentionally separate because the turret rotates
/// independently of the hull.
///
/// <see cref="Type"/> indexes the chassis catalogue (loaded from data at boot);
/// <see cref="Armor"/> and <see cref="Modules"/> carry the live mutable per-instance
/// state that <c>ProjectileHitResolveSystem</c> and <c>ModuleDamageApplySystem</c> update
/// during play. The catalogue tables themselves are static and shared, so they're not
/// duplicated here.
///
/// The force model is stamped from descriptive catalogue fields at spawn time.
/// Velocity, gear, RPM, and angular inertia remain on the entity between fixed ticks.
///
/// Ported from <c>svo/shared/components/hull.h</c>.
/// </summary>
public struct Hull
{
    public TankId Type;

    public Opus.Engine.Physics.Ground.GroundVehicleProperties? Dynamics;

    public Opus.Engine.Physics.Ground.GroundVehicleState DynamicsState;

    public ArmorMap Armor;
    public ModuleMap Modules;
}
