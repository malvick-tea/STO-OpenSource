using System;

namespace Garupan.Content;

/// <summary>Top-level shape of a match: free-for-all (each tank for themselves) or
/// commander-led team play. Drives UI copy + future match-rules + lobby allocation.</summary>
public enum MatchModeKind
{
    /// <summary>Every tank on their own — no teams, last surviving entry wins.</summary>
    FreeForAll,

    /// <summary>Two opposing teams with a single human commander each.</summary>
    TeamTactical,
}

/// <summary>
/// One playable match mode, as listed in the lobby. Authored as data
/// (<c>data/match-modes.csv</c>) so the local test line-up — Hungry Battles (10v10 FFA)
/// and Tactical 5v5 — can grow without touching C#.
/// </summary>
/// <param name="Id">Stable code used by analytics + persistence (e.g. <c>hungry_battles</c>).</param>
/// <param name="Kind">Free-for-all vs. team play; shapes copy and allocation.</param>
/// <param name="NameKey">Translation key for the mode's display name.</param>
/// <param name="SummaryKey">Translation key for the one-paragraph description.</param>
/// <param name="LobbyCapacity">Total players the lobby holds (e.g. 20 for 10v10 FFA,
/// 10 for 5v5 tactical).</param>
/// <param name="RespawnLimit">Number of respawns allowed per player. Zero = single life.</param>
/// <param name="IsCommanderLed">When true, one player per team is the commander with the
/// paper-map briefing tool (Pillar-2 differentiator).</param>
public sealed record MatchMode(
    string Id,
    MatchModeKind Kind,
    string NameKey,
    string SummaryKey,
    int LobbyCapacity,
    int RespawnLimit,
    bool IsCommanderLed)
{
    internal static MatchMode CreateValidated(
        string id,
        MatchModeKind kind,
        string nameKey,
        string summaryKey,
        int lobbyCapacity,
        int respawnLimit,
        bool isCommanderLed)
    {
        EnsureNonEmpty(id, nameof(id));
        EnsureNonEmpty(nameKey, nameof(nameKey));
        EnsureNonEmpty(summaryKey, nameof(summaryKey));
        EnsurePositive(lobbyCapacity, nameof(lobbyCapacity));
        EnsureNonNegative(respawnLimit, nameof(respawnLimit));
        EnsureCommanderConsistency(kind, isCommanderLed);
        return new MatchMode(id, kind, nameKey, summaryKey, lobbyCapacity, respawnLimit, isCommanderLed);
    }

    private static void EnsureNonEmpty(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} must be non-empty.", paramName);
        }
    }

    private static void EnsurePositive(int value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be positive.");
        }
    }

    private static void EnsureNonNegative(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be non-negative.");
        }
    }

    private static void EnsureCommanderConsistency(MatchModeKind kind, bool isCommanderLed)
    {
        if (kind == MatchModeKind.FreeForAll && isCommanderLed)
        {
            throw new ArgumentException(
                "Free-for-all matches have no teams and therefore no commander role.",
                nameof(isCommanderLed));
        }
    }
}
