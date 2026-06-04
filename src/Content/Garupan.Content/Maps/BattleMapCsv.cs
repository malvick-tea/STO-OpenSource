using System;
using System.Collections.Generic;
using System.IO;

namespace Garupan.Content;

/// <summary>Loads the ordered battle-map candidates from <c>content/maps/catalog.csv</c>.
/// Validation is strict: runtime map selection never guesses asset names from conventions.</summary>
public static class BattleMapCsv
{
    private const string Header = "id,model,heightfield,props,obstacles";
    private const int ColumnCount = 5;

    public static BattleMapCatalog LoadFile(string csvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Battle-map CSV not found: {csvPath}", csvPath);
        }

        return Parse(File.ReadAllText(csvPath));
    }

    public static BattleMapCatalog Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        if (lines.Count < 2)
        {
            throw new InvalidDataException("Battle-map CSV must have a header and at least one data row.");
        }

        if (!string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Battle-map CSV header mismatch — expected \"{Header}\", got \"{lines[0]}\".");
        }

        var maps = new List<BattleMapSpec>(lines.Count - 1);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var row = 1; row < lines.Count; row++)
        {
            var map = ParseRow(lines[row], row);
            if (!ids.Add(map.Id))
            {
                throw new InvalidDataException($"Battle-map CSV row {row + 1}: duplicate id \"{map.Id}\".");
            }

            maps.Add(map);
        }

        return new BattleMapCatalog(maps);
    }

    private static BattleMapSpec ParseRow(string line, int row)
    {
        var cells = line.Split(',');
        if (cells.Length != ColumnCount)
        {
            throw new InvalidDataException($"Battle-map CSV row {row + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        try
        {
            return BattleMapSpec.CreateValidated(cells[0], cells[1], cells[2], cells[3], cells[4]);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException($"Battle-map CSV row {row + 1}: {ex.Message}", ex);
        }
    }

    private static List<string> SplitLines(string csv)
    {
        var lines = new List<string>();
        using var reader = new StringReader(csv);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
            {
                lines.Add(trimmed);
            }
        }

        return lines;
    }
}
