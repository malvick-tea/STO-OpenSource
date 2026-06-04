namespace Garupan.Sim.Components;

/// <summary>
/// Per-tank armour layout — runtime per-instance state. Stamped onto the entity at
/// spawn time from the catalogue's armour profile. The six plate values are duplicated
/// onto the entity rather than re-looked-up every hit so the resolver runs as a pure
/// ECS view without taking the catalogue as an argument.
///
/// Sector convention used by the resolver (<see cref="Systems.ProjectileHitResolveSystem"/>):
/// <code>
///                    +X (forward)
///                        ^
///                front  / \  front
///             ----------+----------    +Y axis points right (north)
///             |        / | \        |  in the hull-local frame
///             |  side / O \ side    |  (the hull yaws around O).
///             |      /     \        |
///             ----------+----------
///                rear   \ /  rear
///                        v
///                    -X (rear)
/// </code>
/// The 45° quadrants pick which plate value applies; a hit landing high on the silhouette
/// resolves against the turret plate, otherwise the hull. Each plate carries a mounting slope
/// (degrees from vertical), so the resolver derives effective line-of-sight thickness from the
/// slope and the shot's obliquity rather than comparing raw thickness — a thin sloped glacis
/// (45 mm @ 60°) defends like a far thicker vertical plate.
///
/// Ported from <c>svo/shared/components/armor.h</c>.
/// </summary>
public struct ArmorMap
{
    public float HullFrontMm;
    public float HullFrontSlopeDeg;
    public float HullSideMm;
    public float HullSideSlopeDeg;
    public float HullRearMm;
    public float HullRearSlopeDeg;
    public float TurretFrontMm;
    public float TurretFrontSlopeDeg;
    public float TurretSideMm;
    public float TurretSideSlopeDeg;
    public float TurretRearMm;
    public float TurretRearSlopeDeg;
    public float MantletMm;
    public float MantletSlopeDeg;
    public float RoofMm;
    public float RoofSlopeDeg;
}

/// <summary>
/// Per-tank internal-module roster (engine, transmission, fuel tank, ammo rack, tracks,
/// optics, radio). Phase 0 placeholder: the runtime version tracks per-module hit
/// points, damage state, and the spatial volume the resolver intersects.
/// Empty for now so Hull compiles and the field name is reserved.
///
/// Ported from <c>svo/shared/components/modules.h</c>.
/// </summary>
public struct ModuleMap
{
}
