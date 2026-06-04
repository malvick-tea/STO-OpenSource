namespace Garupan.Sim.Components;

/// <summary>
/// Tag for entities scheduled for removal at end-of-tick. <see cref="CleanupDeadSystem"/>
/// reaps these. Mostly used for spent projectiles after a hit.
/// Ported from <c>svo/shared/components/dead.h</c>.
/// </summary>
public struct Dead
{
}

/// <summary>
/// armored combat� white-flag state: the tank is on the field but inert. Hitting an inert tank
/// is a no-op rather than a double-knock-out. Differs from <see cref="Dead"/>: a
/// KnockedOut tank stays in the world (visible, blocks LOS) but doesn't fight.
/// Ported from <c>svo/shared/components/knocked_out.h</c>.
/// </summary>
public struct KnockedOut
{
}

/// <summary>
/// Hit volume — the radius (metres) of the cylindrical hit-test sphere that projectiles
/// resolve against. Phase 0 single-radius approximation; Phase 1+ swaps for per-plate
/// box collision.
/// Ported from <c>svo/shared/components/hit_radius.h</c>.
/// </summary>
public struct HitRadius
{
    public float Meters;
}

/// <summary>
/// Optional: identifies the entity that fired this projectile. Used to skip self-hits.
/// Ported from <c>svo/shared/components/owner.h</c>.
/// </summary>
public struct Owner
{
    public EntityHandle Entity;
}

/// <summary>
/// Lifetime budget in seconds; <see cref="LifetimeDecaySystem"/> tags this entity as
/// <see cref="Dead"/> when the budget runs out. Used for projectiles that fly off the
/// map without hitting anything.
/// Ported from <c>svo/shared/components/lifetime.h</c>.
/// </summary>
public struct Lifetime
{
    public float SecondsRemaining;
}

/// <summary>
/// Per-tick driving input from a player or AI brain. Throttle [-1, 1], steering [-1, 1].
/// Ported from <c>svo/shared/components/drive_input.h</c>.
/// </summary>
public struct DriveInput
{
    /// <summary>-1 (full reverse) to +1 (full forward). 0 means coast.</summary>
    public float Throttle;

    /// <summary>-1 (left) to +1 (right). 0 means straight.</summary>
    public float Steering;
}

/// <summary>
/// Set when a gunner wants to fire. Cleared by <c>GunFireSystem</c> after the gun shoots.
/// Decoupled from Gun so AI bots and player input can drop intents into the same channel.
/// Ported from <c>svo/shared/components/fire_intent.h</c>.
/// </summary>
public struct FireIntent
{
}

/// <summary>
/// Desired world-frame yaw the turret should aim at. <see cref="Systems.TurretAimSystem"/>
/// rotates the turret toward this value at <see cref="Turret.TraverseSpeedRadPerS"/>.
/// World-frame so AI / player input doesn't have to know the hull yaw to point the gun.
/// Ported from <c>svo/shared/components/turret_target.h</c>.
/// </summary>
public struct TurretTarget
{
    public float YawRadians;
}

/// <summary>
/// Team affiliation. AI / hit-resolve / scoring code branches on this to decide who
/// counts as "us" vs "them". Phase 0 single-player has two teams: PlayerSchool and
/// OpponentSchool. Multi-team matches (RivalDelta 3-way, RivalEcho final w/ flag teams)
/// land later — extend the enum.
/// Ported from <c>svo/shared/components/team.h</c>.
/// </summary>
public enum Team : byte
{
    None = 0,
    PlayerSchool = 1,
    OpponentSchool = 2,
}

/// <summary>Team-affiliation component. Wraps the enum for ECS storage.</summary>
public struct TeamTag
{
    public Team Team;
}

/// <summary>Marker — this entity is the local player's controlled tank.</summary>
public struct PlayerControlled
{
}

/// <summary>Marker — this entity is AI-driven.</summary>
public struct AiControlled
{
}
