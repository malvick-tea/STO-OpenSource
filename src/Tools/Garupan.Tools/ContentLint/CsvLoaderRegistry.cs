using System;
using System.Collections.Generic;
using Garupan.Content;

namespace Garupan.Tools.ContentLint;

/// <summary>One declarative line per known authoring-data CSV. The matcher decides
/// whether a normalised relative path (always forward-slashes) is owned by this entry;
/// the loader runs the parser the runtime uses. New catalogs add one entry — no other
/// content-lint code edits.</summary>
internal sealed record CsvFileMatcher(
    string Description,
    Func<string, bool> MatchesRelativePath,
    Func<string, object> LoadAbsolute);

/// <summary>Single source of truth for which CSV path on disk maps to which loader.
/// Order matters only for human readability — a path matches at most one entry by
/// construction. The campaign loader needs three sentinel identity fields; lint cares
/// about parsing succeeding, not the values, so the sentinels are fixed strings that
/// never collide with real campaign ids.</summary>
internal static class CsvLoaderRegistry
{
    private const string CampaignsPrefix = "campaigns/";
    private const string CrewsPrefix = "crews/";
    private const string CsvSuffix = ".csv";

    public static readonly IReadOnlyList<CsvFileMatcher> All = new[]
    {
        new CsvFileMatcher(
            "school-palette",
            path => Eq(path, "school-palette.csv"),
            absolutePath => SchoolPaletteCsv.LoadFile(absolutePath)),
        new CsvFileMatcher(
            "ai-personalities",
            path => Eq(path, "ai-personalities.csv"),
            absolutePath => BotPersonalityCsv.LoadFile(absolutePath)),
        new CsvFileMatcher(
            "garage-demo-match",
            path => Eq(path, "garage-demo-match.csv"),
            absolutePath => MatchCompositionCsv.LoadFile(absolutePath)),
        new CsvFileMatcher(
            "garage-lighting",
            path => Eq(path, "garage-lighting.csv"),
            absolutePath => LightingPresetCsv.LoadFile(absolutePath)),
        new CsvFileMatcher(
            "shell-visuals",
            path => Eq(path, "shell-visuals.csv"),
            absolutePath => ShellVisualCsv.LoadFile(absolutePath)),
        new CsvFileMatcher(
            "match-modes",
            path => Eq(path, "match-modes.csv"),
            absolutePath => MatchModeCsv.LoadFile(absolutePath)),
        new CsvFileMatcher(
            "campaign",
            path => path.StartsWith(CampaignsPrefix, StringComparison.Ordinal)
                 && path.EndsWith(CsvSuffix, StringComparison.Ordinal),
            absolutePath => CampaignSpecCsv.LoadFile(absolutePath, "lint.id", "lint.name", "lint.subtitle")),
        new CsvFileMatcher(
            "crew",
            path => path.StartsWith(CrewsPrefix, StringComparison.Ordinal)
                 && path.EndsWith(CsvSuffix, StringComparison.Ordinal),
            absolutePath => CrewRosterCsv.LoadFile(absolutePath)),
    };

    public static CsvFileMatcher? FindFor(string normalisedRelativePath)
    {
        ArgumentNullException.ThrowIfNull(normalisedRelativePath);
        foreach (var matcher in All)
        {
            if (matcher.MatchesRelativePath(normalisedRelativePath))
            {
                return matcher;
            }
        }

        return null;
    }

    private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);
}
