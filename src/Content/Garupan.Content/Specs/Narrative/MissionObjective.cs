namespace Garupan.Content;

/// <summary>
/// Win-condition tag. armored combat canon has two real victory conditions: knock out the
/// flag tank, or knock out every enemy. We carry both plus a few campaign-specific
/// extensions (escape, escort) for special-rules matches the anime introduces.
/// </summary>
public enum MissionObjective
{
    /// <summary>Knock out every enemy. Standard "annihilation" match.</summary>
    KnockoutAll,

    /// <summary>Knock out the designated flag tank. Standard "flag-tank" match.</summary>
    CaptureFlagTank,

    /// <summary>Survive an attack, escape encirclement (RivalDelta semifinal canon).</summary>
    BreakOut,

    /// <summary>Endure long enough for a window to open (RivalDelta's snowy stand-off).</summary>
    Survive,

    /// <summary>Tournament round-of-N — multiple opponents, light scripting.</summary>
    Bracket,
}
