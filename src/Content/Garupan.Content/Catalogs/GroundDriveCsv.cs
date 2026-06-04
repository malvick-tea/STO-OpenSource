using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Garupan.Content;

/// <summary>Strict parser for shared tracked-vehicle drivetrain profiles.</summary>
public static class GroundDriveCsv
{
    private const string Header =
        "id,forward_gear_ratios,reverse_gear_ratio,final_drive_ratio,torque_idle_rpm,torque_peak_rpm," +
        "torque_redline_rpm,idle_rpm,upshift_rpm,downshift_rpm,engine_braking_rate_per_second," +
        "maximum_hull_traverse_radians_per_second,turning_resistance_coefficient_seconds";

    private const int ColumnCount = 13;

    public static IReadOnlyList<GroundDriveSpec> Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        RequireHeader(lines);
        var drives = new List<GroundDriveSpec>(lines.Count - 1);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var row = 1; row < lines.Count; row++)
        {
            var drive = ParseRow(lines[row], row);
            if (!ids.Add(drive.Id))
            {
                throw new InvalidDataException($"Ground-drive CSV row {row + 1}: duplicate id \"{drive.Id}\".");
            }

            drives.Add(drive);
        }

        return drives;
    }

    private static GroundDriveSpec ParseRow(string line, int row)
    {
        var cells = line.Split(',');
        if (cells.Length != ColumnCount)
        {
            throw new InvalidDataException(
                $"Ground-drive CSV row {row + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        var torqueIdle = Positive(cells[4], row, "torque_idle_rpm");
        var torquePeak = Positive(cells[5], row, "torque_peak_rpm");
        var torqueRedline = Positive(cells[6], row, "torque_redline_rpm");
        var idle = Positive(cells[7], row, "idle_rpm");
        var upshift = Positive(cells[8], row, "upshift_rpm");
        var downshift = Positive(cells[9], row, "downshift_rpm");
        if (torqueIdle > torquePeak || torquePeak > torqueRedline || downshift >= upshift || upshift > torqueRedline)
        {
            throw new InvalidDataException($"Ground-drive CSV row {row + 1}: RPM ranges are inconsistent.");
        }

        return new GroundDriveSpec(
            Require(cells[0], row, "id"),
            PositiveList(cells[1], row, "forward_gear_ratios"),
            Positive(cells[2], row, "reverse_gear_ratio"),
            Positive(cells[3], row, "final_drive_ratio"),
            torqueIdle,
            torquePeak,
            torqueRedline,
            idle,
            upshift,
            downshift,
            NonNegative(cells[10], row, "engine_braking_rate_per_second"),
            Positive(cells[11], row, "maximum_hull_traverse_radians_per_second"),
            NonNegative(cells[12], row, "turning_resistance_coefficient_seconds"));
    }

    private static IReadOnlyList<double> PositiveList(string cell, int row, string name)
    {
        var parts = cell.Split('|');
        if (parts.Length == 0)
        {
            throw new InvalidDataException($"Ground-drive CSV row {row + 1}: column \"{name}\" is empty.");
        }

        var values = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            values[i] = Positive(parts[i], row, name);
        }

        return values;
    }

    private static double Positive(string cell, int row, string name)
    {
        var value = Finite(cell, row, name);
        if (value <= 0)
        {
            throw new InvalidDataException($"Ground-drive CSV row {row + 1}: column \"{name}\" must be positive.");
        }

        return value;
    }

    private static double NonNegative(string cell, int row, string name)
    {
        var value = Finite(cell, row, name);
        if (value < 0)
        {
            throw new InvalidDataException($"Ground-drive CSV row {row + 1}: column \"{name}\" must be non-negative.");
        }

        return value;
    }

    private static double Finite(string cell, int row, string name)
    {
        if (!double.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value))
        {
            throw new InvalidDataException($"Ground-drive CSV row {row + 1}: column \"{name}\" is not finite.");
        }

        return value;
    }

    private static string Require(string cell, int row, string name)
    {
        var value = cell.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidDataException($"Ground-drive CSV row {row + 1}: column \"{name}\" is empty.");
        }

        return value;
    }

    private static void RequireHeader(IReadOnlyList<string> lines)
    {
        if (lines.Count < 2 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Ground-drive CSV header mismatch or no data rows.");
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
