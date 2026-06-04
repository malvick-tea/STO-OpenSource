using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Garupan.Content;

/// <summary>Strict parser for shared gun-installation geometry profiles.</summary>
public static class GunMountCsv
{
    private const string Header =
        "id,min_pitch_degrees,max_pitch_degrees,trunnion_forward_meters,trunnion_height_meters," +
        "barrel_length_meters";

    private const int ColumnCount = 6;

    public static IReadOnlyList<GunMountSpec> Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        RequireHeader(lines);
        var mounts = new List<GunMountSpec>(lines.Count - 1);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var row = 1; row < lines.Count; row++)
        {
            var mount = ParseRow(lines[row], row);
            if (!ids.Add(mount.Id))
            {
                throw new InvalidDataException($"Gun-mount CSV row {row + 1}: duplicate id \"{mount.Id}\".");
            }

            mounts.Add(mount);
        }

        return mounts;
    }

    private static GunMountSpec ParseRow(string line, int row)
    {
        var cells = line.Split(',');
        if (cells.Length != ColumnCount)
        {
            throw new InvalidDataException(
                $"Gun-mount CSV row {row + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        var minPitch = Finite(cells[1], row, "min_pitch_degrees");
        var maxPitch = Finite(cells[2], row, "max_pitch_degrees");
        if (minPitch >= maxPitch)
        {
            throw new InvalidDataException($"Gun-mount CSV row {row + 1}: pitch range must increase.");
        }

        return new GunMountSpec(
            Require(cells[0], row, "id"),
            minPitch,
            maxPitch,
            NonNegative(cells[3], row, "trunnion_forward_meters"),
            NonNegative(cells[4], row, "trunnion_height_meters"),
            Positive(cells[5], row, "barrel_length_meters"));
    }

    private static double Positive(string cell, int row, string name)
    {
        var value = Finite(cell, row, name);
        if (value <= 0)
        {
            throw new InvalidDataException($"Gun-mount CSV row {row + 1}: column \"{name}\" must be positive.");
        }

        return value;
    }

    private static double NonNegative(string cell, int row, string name)
    {
        var value = Finite(cell, row, name);
        if (value < 0)
        {
            throw new InvalidDataException($"Gun-mount CSV row {row + 1}: column \"{name}\" must be non-negative.");
        }

        return value;
    }

    private static double Finite(string cell, int row, string name)
    {
        if (!double.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value))
        {
            throw new InvalidDataException($"Gun-mount CSV row {row + 1}: column \"{name}\" is not finite.");
        }

        return value;
    }

    private static string Require(string cell, int row, string name)
    {
        var value = cell.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidDataException($"Gun-mount CSV row {row + 1}: column \"{name}\" is empty.");
        }

        return value;
    }

    private static void RequireHeader(IReadOnlyList<string> lines)
    {
        if (lines.Count < 2 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Gun-mount CSV header mismatch or no data rows.");
        }
    }

    private static List<string> SplitLines(string csv)
    {
        var lines = new List<string>();
        using var reader = new StringReader(csv);
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        return lines;
    }
}
