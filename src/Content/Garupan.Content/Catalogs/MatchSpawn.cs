using System.Numerics;

namespace Garupan.Content;

/// <summary>
/// One scripted spawn in a match composition: which <c>TankSpec.Id</c> spawns, what
/// <see cref="MatchRole"/> it plays (the local player or an AI opponent), and where /
/// facing what bearing it starts the match. Composed by <see cref="MatchComposition"/>
/// and authored in <c>data/*.csv</c> match-layout files.
/// </summary>
public readonly record struct MatchSpawn(
    string TankId,
    MatchRole Role,
    Vector2 Position,
    float YawRadians);

/// <summary>Distinguishes the player's own spawn from AI opponent spawns in a
/// <see cref="MatchComposition"/>. Phase 0 has exactly one Player; runtime matches
/// could allow N-player co-op or PvP (per the 30v30 pillar) by relaxing that invariant.</summary>
public enum MatchRole
{
    Player,
    Opponent,
}
