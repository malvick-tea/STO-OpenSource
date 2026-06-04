using System;
using System.Collections.Generic;
using System.IO;
using Garupan.Content;

namespace Garupan.Tools.ContentLint;

/// <summary>Walks every <c>*.csv</c> under a data root and dispatches each to the
/// matching loader from <see cref="CsvLoaderRegistry"/>. Records parse errors per file
/// without aborting the pass — a content-lint sweep should report every broken file
/// in one shot, not stop at the first.</summary>
internal sealed class CsvLoadRunner
{
    public CsvLoadResult Run(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        var parseErrors = new Dictionary<string, string>(StringComparer.Ordinal);
        var unmatched = new List<string>();
        SchoolPalette? loadedPalette = null;

        if (!Directory.Exists(dataDirectory))
        {
            return new CsvLoadResult(parseErrors, unmatched, loadedPalette, DirectoryMissing: true);
        }

        foreach (var absolute in Directory.EnumerateFiles(dataDirectory, "*.csv", SearchOption.AllDirectories))
        {
            var relative = NormalisePath(Path.GetRelativePath(dataDirectory, absolute));
            var matcher = CsvLoaderRegistry.FindFor(relative);
            if (matcher is null)
            {
                unmatched.Add(relative);
                continue;
            }

            try
            {
                var loaded = matcher.LoadAbsolute(absolute);
                if (loaded is SchoolPalette palette)
                {
                    loadedPalette = palette;
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or FormatException or ArgumentException)
            {
                parseErrors[relative] = ex.Message;
            }
        }

        return new CsvLoadResult(parseErrors, unmatched, loadedPalette, DirectoryMissing: false);
    }

    /// <summary>Forward-slash everywhere so matchers don't have to branch on Windows
    /// vs POSIX separators.</summary>
    private static string NormalisePath(string relative) => relative.Replace('\\', '/');
}

internal sealed record CsvLoadResult(
    IReadOnlyDictionary<string, string> ParseErrors,
    IReadOnlyList<string> UnmatchedCsvFiles,
    SchoolPalette? LoadedPalette,
    bool DirectoryMissing);
