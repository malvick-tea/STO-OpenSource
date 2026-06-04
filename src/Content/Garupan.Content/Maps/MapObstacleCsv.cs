using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace Garupan.Content;

/// <summary>
/// Parses a map's static-obstacle table (<c>content/maps/&lt;name&gt;-obstacles.csv</c>, emitted by
/// the city generator) into <see cref="MapObstacle"/> rows — the impassable building footprints the
/// simulation blocks tanks against. Per the no-hardcode rule a map's collision is authoring data
/// emitted alongside its visual mesh, never C#, so regenerating the city re-derives the colliders
/// with no recompile.
/// </summary>
/// <remarks>
/// Schema (6 columns): <c>x,z,yaw,half_w_m,half_d_m,height_m</c>. Blank lines and <c>#</c> comment
/// lines (the generator stamps a provenance comment) are ignored. Validation is strict — a wrong
/// column count or a non-numeric / non-positive extent surfaces at load with row context, so a
/// malformed table fails loudly instead of spawning degenerate colliders.
/// </remarks>
public static class MapObstacleCsv
{
    private const string Header = "x,z,yaw,half_w_m,half_d_m,height_m";
    private const int ColumnCount = 6;

    public static IReadOnlyList<MapObstacle> LoadFile(string csvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Map-obstacles CSV not found: {csvPath}", csvPath);
        }

        return Parse(File.ReadAllText(csvPath));
    }

    public static IReadOnlyList<MapObstacle> Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        if (lines.Count < 1)
        {
            throw new InvalidDataException("Map-obstacles CSV must have at least a header row.");
        }

        if (!string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Map-obstacles CSV header mismatch — expected \"{Header}\", got \"{lines[0]}\".");
        }

        var obstacles = new List<MapObstacle>(lines.Count - 1);
        for (var row = 1; row < lines.Count; row++)
        {
            obstacles.Add(ParseRow(lines[row], row));
        }

        return obstacles;
    }

    private static MapObstacle ParseRow(string line, int row)
    {
        var cells = line.Split(',');
        if (cells.Length != ColumnCount)
        {
            throw new InvalidDataException($"Map-obstacles CSV row {row + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        return new MapObstacle(
            new Vector2(ParseFloat(cells[0], row, "x"), ParseFloat(cells[1], row, "z")),
            ParseFloat(cells[2], row, "yaw"),
            ParsePositive(cells[3], row, "half_w_m"),
            ParsePositive(cells[4], row, "half_d_m"),
            ParsePositive(cells[5], row, "height_m"));
    }

    private static float ParseFloat(string cell, int row, string columnName) =>
        float.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && float.IsFinite(value)
            ? value
            : throw new InvalidDataException($"Map-obstacles CSV row {row + 1}: column \"{columnName}\" is not a finite number (\"{cell.Trim()}\").");

    private static float ParsePositive(string cell, int row, string columnName)
    {
        var value = ParseFloat(cell, row, columnName);
        if (value <= 0f)
        {
            throw new InvalidDataException($"Map-obstacles CSV row {row + 1}: column \"{columnName}\" must be positive, got {value}.");
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
