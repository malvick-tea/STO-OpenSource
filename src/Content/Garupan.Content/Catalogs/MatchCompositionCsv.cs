using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace Garupan.Content;

/// <summary>
/// Parses <c>data/*.csv</c> match composition files into a <see cref="MatchComposition"/>.
/// Schema: header row + N data rows, columns <c>tank_id,role,pos_x,pos_y,yaw_radians</c>.
/// Tank id strings are resolved against <see cref="TankRoster"/> by the loader so a typo
/// surfaces immediately; roles are case-insensitive (<c>player</c> / <c>opponent</c>).
/// </summary>
public static class MatchCompositionCsv
{
    private const string Header = "tank_id,role,pos_x,pos_y,yaw_radians";
    private const int ColumnCount = 5;

    public static MatchComposition LoadFile(string csvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Match composition CSV not found: {csvPath}", csvPath);
        }

        return Parse(File.ReadAllText(csvPath));
    }

    public static MatchComposition Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        if (lines.Count < 2)
        {
            throw new InvalidDataException("Match composition CSV must have at least a header row and one data row.");
        }

        if (!string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Match composition CSV header mismatch — expected \"{Header}\", got \"{lines[0]}\".");
        }

        var spawns = new List<MatchSpawn>(lines.Count - 1);
        for (var row = 1; row < lines.Count; row++)
        {
            spawns.Add(ParseRow(lines[row], row));
        }

        return new MatchComposition(spawns);
    }

    private static MatchSpawn ParseRow(string line, int rowIndex)
    {
        var cells = line.Split(',', ColumnCount);
        if (cells.Length < ColumnCount)
        {
            throw new InvalidDataException(
                $"Match composition CSV row {rowIndex + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        var tankId = cells[0].Trim();
        if (string.IsNullOrEmpty(tankId))
        {
            throw new InvalidDataException(
                $"Match composition CSV row {rowIndex + 1}: tank_id is empty.");
        }

        if (TankRoster.FindById(tankId) is null)
        {
            throw new InvalidDataException(
                $"Match composition CSV row {rowIndex + 1}: tank_id \"{tankId}\" does not resolve in TankRoster.");
        }

        var role = ParseRole(cells[1], rowIndex);
        var posX = ParseFloat(cells[2], rowIndex, "pos_x");
        var posY = ParseFloat(cells[3], rowIndex, "pos_y");
        var yaw = ParseFloat(cells[4], rowIndex, "yaw_radians");
        return new MatchSpawn(tankId, role, new Vector2(posX, posY), yaw);
    }

    private static MatchRole ParseRole(string cell, int rowIndex)
    {
        if (!Enum.TryParse<MatchRole>(cell.Trim(), ignoreCase: true, out var parsed))
        {
            throw new InvalidDataException(
                $"Match composition CSV row {rowIndex + 1}: unknown role \"{cell}\" (expected player or opponent).");
        }

        return parsed;
    }

    private static float ParseFloat(string cell, int rowIndex, string columnName)
    {
        if (!float.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidDataException(
                $"Match composition CSV row {rowIndex + 1}: column \"{columnName}\" is not a valid float (\"{cell}\").");
        }

        if (!float.IsFinite(value))
        {
            throw new InvalidDataException(
                $"Match composition CSV row {rowIndex + 1}: column \"{columnName}\" must be finite (got {value}).");
        }

        return value;
    }

    private static List<string> SplitLines(string csv)
    {
        var lines = new List<string>();
        using var reader = new StringReader(csv);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }

        return lines;
    }
}
