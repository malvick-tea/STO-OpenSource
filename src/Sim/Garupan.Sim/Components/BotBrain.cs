namespace Garupan.Sim.Components;

/// <summary>
/// Tag-plus-config the AI loop reads. Its presence marks an entity as AI-controlled and
/// the field values tune the policy. Phase 0 ships exactly one bot policy ("seek nearest
/// enemy inside engage range, drive toward them, fire when aligned"), so the only knob
/// is the engagement distance; per-school personality modules (RivalDelta's patience,
/// RivalEcho's pressure, etc.) land in M5+ per ADR-0008 as additional fields on this
/// struct or as sibling components.
///
/// Replaces the previous hard-coded <c>StopDistanceMeters</c> constant in
/// <see cref="Systems.AiBotSystem"/> — same number lives on the entity now so per-tank
/// tuning and future per-school overrides are a data change, not a code change.
///
/// Ported from <c>svo/shared/components/bot_brain.h</c>.
/// </summary>
public struct BotBrain
{
    /// <summary>Distance, metres, beyond which the bot will not pick a target. C++ default
    /// is 60 m — long enough that bots open fire before colliding, short enough that
    /// off-screen targets don't get free shots.</summary>
    public float EngageRangeMeters;
}
