using System;
using System.Collections.Generic;

namespace Garupan.Content;

/// <summary>
/// Loaded per-school AI personality catalog. Produced by
/// <see cref="BotPersonalityCsv.LoadFile"/> / <see cref="BotPersonalityCsv.Parse"/> from
/// <c>data/ai-personalities.csv</c>. The Sim layer queries this via
/// <see cref="Resolve"/> at spawn time to stamp the right <c>BotBrain</c> on each AI tank.
/// </summary>
/// <remarks>
/// <para>
/// Lookup semantics: <see cref="Resolve"/> returns the matching personality when the
/// school is present, and a <see cref="BotPersonality.LegacyFallback"/>-shaped record
/// (re-tagged with the requested school) when it is not. This keeps an incomplete CSV
/// or a school added to the roster before the personality table catches up from
/// stalling the boot — the bot simply behaves with the M3 / M4 defaults until a
/// designer fills in a row.
/// </para>
/// <para>
/// Use <see cref="Contains"/> + <see cref="CatalogValidator"/> at boot to assert full
/// coverage when a release build wants no implicit fallbacks.
/// </para>
/// </remarks>
public sealed class BotPersonalityCatalog
{
    private readonly Dictionary<OpponentSchool, BotPersonality> _bySchool;

    internal BotPersonalityCatalog(Dictionary<OpponentSchool, BotPersonality> bySchool)
    {
        ArgumentNullException.ThrowIfNull(bySchool);
        _bySchool = bySchool;
    }

    /// <summary>Number of schools defined in this catalog.</summary>
    public int Count => _bySchool.Count;

    /// <summary>Whether this catalog has an entry for <paramref name="school"/>.</summary>
    public bool Contains(OpponentSchool school) => _bySchool.ContainsKey(school);

    /// <summary>Returns the personality for <paramref name="school"/>, or a fallback
    /// personality with the requested school stamped on it when no entry exists.</summary>
    public BotPersonality Resolve(OpponentSchool school)
    {
        if (_bySchool.TryGetValue(school, out var personality))
        {
            return personality;
        }

        return BotPersonality.LegacyFallback with { School = school };
    }

    /// <summary>Enumerates every personality in declaration order from the CSV.</summary>
    public IEnumerable<BotPersonality> All => _bySchool.Values;
}
