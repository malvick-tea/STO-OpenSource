using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Garupan.Content;

/// <summary>
/// Loaded list of playable match modes. Produced by <see cref="MatchModeCsv.LoadFile"/>
/// from <c>data/match-modes.csv</c>; consumed by the lobby UI for mode selection and by
/// future server / matchmaker code to validate join requests.
/// </summary>
/// <remarks>
/// Modes are surfaced in declaration order — the CSV row order is the canonical lobby
/// order, which keeps designers in control of the menu without code changes.
/// </remarks>
public sealed class MatchModeCatalog
{
    private readonly Dictionary<string, MatchMode> _byId;

    internal MatchModeCatalog(IReadOnlyList<MatchMode> modes)
    {
        Modes = new ReadOnlyCollection<MatchMode>(new List<MatchMode>(modes));
        _byId = new Dictionary<string, MatchMode>(modes.Count, StringComparer.Ordinal);
        foreach (var mode in modes)
        {
            _byId.Add(mode.Id, mode);
        }
    }

    /// <summary>All modes in CSV-declaration order.</summary>
    public IReadOnlyList<MatchMode> Modes { get; }

    /// <summary>Number of modes in the catalog.</summary>
    public int Count => Modes.Count;

    /// <summary>True when the catalog has a mode with the given id.</summary>
    public bool Contains(string id) => id is not null && _byId.ContainsKey(id);

    /// <summary>Looks up a mode by id; returns <c>null</c> when absent.</summary>
    public MatchMode? Find(string id) =>
        id is not null && _byId.TryGetValue(id, out var mode) ? mode : null;
}
