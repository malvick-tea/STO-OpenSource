using Garupan.Content;

namespace Garupan.Sim.Components;

/// <summary>Lifecycle of a destructible map prop.</summary>
public enum PropState
{
    /// <summary>Upright and intact — blocks tanks that cannot defeat it.</summary>
    Standing,

    /// <summary>Hinging over after being felled — animating, no longer blocks.</summary>
    Toppling,

    /// <summary>Down on the ground, a felled trunk — inert, no longer blocks.</summary>
    Fallen,

    /// <summary>Shattered and removed — inert, no longer blocks.</summary>
    Broken,
}

/// <summary>
/// A destructible map object as a simulation entity: a static obstacle that blocks tanks while
/// <see cref="PropState.Standing"/>, until an impactor delivers enough energy (a charging hull)
/// or push force (a strong hull leaning on it) to fail its base — after which it topples or
/// breaks per <see cref="Behavior"/>. The two thresholds and the mass are stamped from the
/// prop's physical size + material at spawn (<see cref="Spawn.MapPropSpawner"/>), so the
/// collision system only compares numbers — no per-prop tuning, the whole city's clutter
/// follows from its catalogue dimensions.
/// </summary>
public struct DestructibleProp
{
    /// <summary>Stable identity of this prop within its map — the zero-based row index of the
    /// prop in the map's <c>-props.csv</c>, stamped at spawn (<see cref="Spawn.MapPropSpawner"/>).
    /// The client loads the same CSV in the same order, so this id lets the snapshot replicate a
    /// felled prop's state to the exact box the client is drawing without sending its geometry.</summary>
    public int PropId;

    public PropKind Kind;
    public PropBehavior Behavior;
    public PropState State;

    /// <summary>Impactor kinetic energy (J) that fails the base — the charge-through criterion.</summary>
    public float ToppleEnergyJoules;

    /// <summary>Horizontal force (N) the base resists — the static push-over criterion: a tank
    /// whose traction-limited drive exceeds this fells the prop from a standstill.</summary>
    public float ResistingForceNewtons;

    /// <summary>Planar contact radius (m) — the base half-width a tank must reach to touch it.</summary>
    public float RadiusMeters;

    public float MassKg;

    /// <summary>World heading the prop falls toward once toppling — the impactor's travel
    /// direction at the moment of failure. Drives the eventual client fall animation.</summary>
    public float FallYawRadians;

    /// <summary>Seconds elapsed in the current transient state (used to age <see cref="PropState.Toppling"/>
    /// into <see cref="PropState.Fallen"/>).</summary>
    public float StateSeconds;
}
