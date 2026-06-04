using System;
using System.Collections.Generic;
using System.Linq;

namespace Garupan.Content;

/// <summary>
/// Ordered list of <see cref="MatchSpawn"/> entries that defines a complete match
/// composition: exactly one player spawn + zero-or-more opponent spawns. Loaded from
/// <c>data/*.csv</c> by <see cref="MatchCompositionCsv"/> so writers / mission designers
/// can author new match layouts without recompiling C#. Phase A canon missions, the
/// Garage demo, replay playback, and future scripted-encounter tools all consume this
/// shape.
/// </summary>
/// <remarks>
/// Invariant: a valid composition has exactly one <see cref="MatchRole.Player"/> entry
/// (Phase A single-player). The constructor enforces it; loaders that produce zero or
/// multiple players fail validation rather than silently picking the first.
/// </remarks>
public sealed class MatchComposition
{
    public MatchComposition(IReadOnlyList<MatchSpawn> spawns)
    {
        ArgumentNullException.ThrowIfNull(spawns);
        var playerCount = spawns.Count(s => s.Role == MatchRole.Player);
        if (playerCount != 1)
        {
            throw new ArgumentException(
                $"MatchComposition must have exactly one Player spawn (found {playerCount}).",
                nameof(spawns));
        }

        Spawns = spawns;
    }

    /// <summary>Every spawn in author-order. Loaders preserve the CSV row order so the
    /// player can rely on opponent indices matching the file's row order for replay /
    /// commentary purposes.</summary>
    public IReadOnlyList<MatchSpawn> Spawns { get; }

    /// <summary>The single <see cref="MatchRole.Player"/> spawn — the local player's
    /// starting position + tank.</summary>
    public MatchSpawn Player => Spawns.First(s => s.Role == MatchRole.Player);

    /// <summary>Every <see cref="MatchRole.Opponent"/> spawn in author-order. Equal to
    /// <see cref="Spawns"/> minus the player.</summary>
    public IEnumerable<MatchSpawn> Opponents => Spawns.Where(s => s.Role == MatchRole.Opponent);
}
