using System;
using System.Collections.Generic;

namespace Garupan.Content;

/// <summary>
/// Loaded crew catalog for one faction's team (e.g. the player faction's crew). Produced
/// by <see cref="CrewRosterCsv.LoadFile"/> / <see cref="CrewRosterCsv.Parse"/> from
/// <c>data/crews/&lt;team&gt;.csv</c>. The player crew's CSV ships at
/// <c>data/crews/player_crew.csv</c>.
/// </summary>
/// <remarks>
/// <para>
/// Holds an immutable list of <see cref="CrewMember"/> in canon seating order (commander
/// first, outward through the tank). <see cref="FindById"/> resolves by stable id;
/// <see cref="Contains"/> tests presence without throwing.
/// </para>
/// <para>
/// Why a loaded class rather than a static catalog: per ADR-0030 authoring data lives
/// in CSVs, and per the campaign-pillar memo the writers should not touch C#. Crew
/// rosters are authoring data — adding / removing / re-roling a member is one CSV edit.
/// </para>
/// </remarks>
public sealed class CrewRoster
{
    private readonly Dictionary<string, CrewMember> _byId;

    internal CrewRoster(IReadOnlyList<CrewMember> all, string schoolKey)
    {
        ArgumentNullException.ThrowIfNull(all);
        ArgumentException.ThrowIfNullOrWhiteSpace(schoolKey);
        All = all;
        SchoolKey = schoolKey;
        _byId = new Dictionary<string, CrewMember>(all.Count, StringComparer.Ordinal);
        foreach (var m in all)
        {
            _byId[m.Id] = m;
        }
    }

    /// <summary>Crew members in canon seating order (commander → outward).</summary>
    public IReadOnlyList<CrewMember> All { get; }

    /// <summary>The school every member of this roster belongs to. Matches the
    /// <see cref="OpponentSchool"/> enum's lowercase identifier (e.g. <c>"player_school"</c>).</summary>
    public string SchoolKey { get; }

    /// <summary>Resolves a member by id, or null when unknown.</summary>
    public CrewMember? FindById(string id) =>
        _byId.TryGetValue(id, out var found) ? found : null;

    /// <summary>Whether the roster contains a member with the given id.</summary>
    public bool Contains(string id) => _byId.ContainsKey(id);
}
