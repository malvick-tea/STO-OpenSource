using System;
using System.Collections.Generic;
using System.IO;

namespace Garupan.Tools.LocLint;

/// <summary>Collects translation-key references buried in data CSVs. Garupan's
/// authoring data (campaigns / crews / future scripted-beat catalogs) stores
/// translation keys in columns whose name ends in <c>_key</c>: <c>title_key</c>,
/// <c>briefing_key</c>, <c>lore_key</c>, etc. The scanner walks every <c>.csv</c>
/// under the supplied directory (recursive) and pulls referenced strings out of those
/// columns so loc-lint flags any unlocalised reference at build time.</summary>
/// <remarks>
/// Translation keys in this project are dotted identifiers (<c>menu.title</c>,
/// <c>campaign.sample.prefectural.briefing</c>); values in <c>_key</c> columns that
/// contain no dot are treated as short identifiers (e.g. <c>school_key=player_school</c>
/// referencing the school registry) and skipped. This stays heuristic on purpose —
/// a strict mode that opts out of the heuristic would be cheap to add later but isn't
/// needed pre-alpha.
/// </remarks>
internal sealed class DataCsvKeyScanner
{
    private const StringComparison HeaderCmp = StringComparison.OrdinalIgnoreCase;
    private const string KeyColumnSuffix = "_key";
    private const char TranslationKeySeparator = '.';

    /// <summary>Returns the deduplicated set of translation keys referenced by every
    /// <c>*.csv</c> under <paramref name="dataDirectory"/>. A non-existent directory
    /// returns an empty set — the caller decides whether to treat that as an error
    /// separately.</summary>
    public IReadOnlyCollection<string> Collect(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(dataDirectory))
        {
            return keys;
        }

        foreach (var csvPath in Directory.EnumerateFiles(dataDirectory, "*.csv", SearchOption.AllDirectories))
        {
            CollectFromFile(csvPath, keys);
        }

        return keys;
    }

    private static void CollectFromFile(string csvPath, HashSet<string> keys)
    {
        using var reader = new StreamReader(csvPath);
        var headerLine = ReadNextDataLine(reader);
        if (headerLine is null)
        {
            return;
        }

        var headers = headerLine.Split(',');
        var keyColumnIndices = new List<int>();
        for (var i = 0; i < headers.Length; i++)
        {
            if (headers[i].EndsWith(KeyColumnSuffix, HeaderCmp))
            {
                keyColumnIndices.Add(i);
            }
        }

        if (keyColumnIndices.Count == 0)
        {
            return;
        }

        string? line;
        while ((line = ReadNextDataLine(reader)) is not null)
        {
            var cells = line.Split(',');
            foreach (var idx in keyColumnIndices)
            {
                if (idx >= cells.Length)
                {
                    continue;
                }

                var raw = cells[idx].Trim();
                if (raw.Length == 0)
                {
                    continue;
                }

                foreach (var split in raw.Split('|'))
                {
                    var token = split.Trim();
                    if (token.Length > 0 && token.Contains(TranslationKeySeparator, StringComparison.Ordinal))
                    {
                        keys.Add(token);
                    }
                }
            }
        }
    }

    private static string? ReadNextDataLine(StreamReader reader)
    {
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            return line;
        }

        return null;
    }
}
