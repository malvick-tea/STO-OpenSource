namespace Garupan.Content;

/// <summary>
/// The factions that compete in the campaign. <see cref="PlayerSchool"/> is the
/// player's own faction; the remaining entries are rival factions the player faces
/// across the campaign. Add a faction here when the roster grows.
/// </summary>
public enum OpponentSchool
{
    /// <summary>The player's own faction.</summary>
    PlayerSchool,

    /// <summary>Rival faction. Balanced all-round doctrine.</summary>
    RivalAlpha,

    /// <summary>Rival faction. Numbers-and-firepower doctrine.</summary>
    RivalBravo,

    /// <summary>Rival faction. Aggressive close-range doctrine.</summary>
    RivalCharlie,

    /// <summary>Rival faction. Patient defensive doctrine.</summary>
    RivalDelta,

    /// <summary>Rival faction. Heavy-armour doctrine.</summary>
    RivalEcho,

    /// <summary>Rival faction (introduced later).</summary>
    RivalFoxtrot,

    /// <summary>Rival faction (introduced later).</summary>
    RivalGolf,
}
