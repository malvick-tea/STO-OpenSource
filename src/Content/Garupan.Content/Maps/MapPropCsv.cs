using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace Garupan.Content;

/// <summary>
/// Parses a map's destructible-prop table (<c>content/maps/&lt;name&gt;-props.csv</c>, emitted by
/// <c>tools/mapgen/extract-city-props.py</c>) into <see cref="MapProp"/> rows. Per the no-hardcode
/// rule a map's clutter is authoring data extracted from the source model, never C# — so swapping
/// the city model re-runs the separator and the simulation picks up the new props with no recompile.
/// </summary>
/// <remarks>
/// Schema (6 columns): <c>kind,x,z,yaw,base_diameter_m,height_m</c>. Blank lines and <c>#</c>
/// comment lines (the separator stamps a provenance comment) are ignored. Validation is strict —
/// a wrong column count, an unknown kind, or a non-numeric / negative size surfaces at load with
/// row context, so a malformed extraction fails loudly instead of spawning broken props.
/// </remarks>
public static class MapPropCsv
{
    private const string Header = "kind,x,z,yaw,base_diameter_m,height_m";
    private const int ColumnCount = 6;

    public static IReadOnlyList<MapProp> LoadFile(string csvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Map-props CSV not found: {csvPath}", csvPath);
        }

        return Parse(File.ReadAllText(csvPath));
    }

    public static IReadOnlyList<MapProp> Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        if (lines.Count < 1)
        {
            throw new InvalidDataException("Map-props CSV must have at least a header row.");
        }

        if (!string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Map-props CSV header mismatch — expected \"{Header}\", got \"{lines[0]}\".");
        }

        var props = new List<MapProp>(lines.Count - 1);
        for (var row = 1; row < lines.Count; row++)
        {
            props.Add(ParseRow(lines[row], row));
        }

        return props;
    }

    private static MapProp ParseRow(string line, int row)
    {
        var cells = line.Split(',');
        if (cells.Length != ColumnCount)
        {
            throw new InvalidDataException($"Map-props CSV row {row + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        return new MapProp(
            ParseKind(cells[0], row),
            new Vector2(ParseFloat(cells[1], row, "x"), ParseFloat(cells[2], row, "z")),
            ParseFloat(cells[3], row, "yaw"),
            ParsePositive(cells[4], row, "base_diameter_m"),
            ParsePositive(cells[5], row, "height_m"));
    }

    private static PropKind ParseKind(string cell, int row) =>
        Enum.TryParse<PropKind>(cell.Trim(), ignoreCase: true, out var kind)
            ? kind
            : throw new InvalidDataException($"Map-props CSV row {row + 1}: unknown prop kind \"{cell.Trim()}\".");

    private static float ParseFloat(string cell, int row, string columnName) =>
        float.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && float.IsFinite(value)
            ? value
            : throw new InvalidDataException($"Map-props CSV row {row + 1}: column \"{columnName}\" is not a finite number (\"{cell.Trim()}\").");

    private static float ParsePositive(string cell, int row, string columnName)
    {
        var value = ParseFloat(cell, row, columnName);
        if (value <= 0f)
        {
            throw new InvalidDataException($"Map-props CSV row {row + 1}: column \"{columnName}\" must be positive, got {value}.");
        }

        return value;
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
