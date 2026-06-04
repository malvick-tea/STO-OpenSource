namespace Garupan.Content;

/// <summary>
/// Coarse environment tag for a match. Drives backdrop / lighting choice in the briefing
/// + match scenes; mission designers pick from a small enum so we can ship maps in
/// batches rather than one per mission.
/// </summary>
public enum MissionEnvironment
{
    /// <summary>Open countryside, dirt roads — RivalBravo, prefectural qualifier.</summary>
    RuralOpen,

    /// <summary>Forested ridges + valleys — St. RivalAlpha practice match.</summary>
    ForestedHills,

    /// <summary>Snowy abandoned village + fields — RivalDelta semifinal.</summary>
    SnowyVillage,

    /// <summary>Mountain pass with switchbacks — used for RivalEcho final.</summary>
    Mountain,

    /// <summary>Italian-coast town with narrow streets — RivalCharlie.</summary>
    CoastalTown,

    /// <summary>Industrial outskirts, abandoned factory yards.</summary>
    Industrial,
}
