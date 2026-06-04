using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Garupan.Content;

/// <summary>Strict parser for shared gun envelopes and their recoil mechanisms.</summary>
public static class GunCsv
{
    private const string Header =
        "id,caliber,penetration_mm,damage,reload_seconds,rounds_per_minute,default_ammo_id," +
        "recoiling_assembly_mass_kg,maximum_recoil_travel_meters,recoil_brake_force_newtons," +
        "muzzle_brake_efficiency,recoil_return_seconds";

    private const int ColumnCount = 12;

    public static IReadOnlyList<GunSpec> Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        RequireHeader(lines);
        var guns = new List<GunSpec>(lines.Count - 1);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var row = 1; row < lines.Count; row++)
        {
            var gun = ParseRow(lines[row], row);
            if (!ids.Add(gun.Id))
            {
                throw new InvalidDataException($"Gun CSV row {row + 1}: duplicate id \"{gun.Id}\".");
            }

            guns.Add(gun);
        }

        return guns;
    }

    private static GunSpec ParseRow(string line, int row)
    {
        var cells = line.Split(',');
        if (cells.Length != ColumnCount)
        {
            throw new InvalidDataException($"Gun CSV row {row + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        return new GunSpec(
            Require(cells[0], row, "id"),
            Require(cells[1], row, "caliber"),
            PositiveInt(cells[2], row, "penetration_mm"),
            PositiveInt(cells[3], row, "damage"),
            PositiveDouble(cells[4], row, "reload_seconds"),
            PositiveInt(cells[5], row, "rounds_per_minute"),
            Require(cells[6], row, "default_ammo_id"),
            PositiveDouble(cells[7], row, "recoiling_assembly_mass_kg"),
            PositiveDouble(cells[8], row, "maximum_recoil_travel_meters"),
            PositiveDouble(cells[9], row, "recoil_brake_force_newtons"),
            UnitInterval(cells[10], row, "muzzle_brake_efficiency"),
            PositiveDouble(cells[11], row, "recoil_return_seconds"));
    }

    private static int PositiveInt(string cell, int row, string name)
    {
        if (!int.TryParse(cell.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            throw new InvalidDataException($"Gun CSV row {row + 1}: column \"{name}\" must be a positive integer.");
        }

        return value;
    }

    private static double PositiveDouble(string cell, int row, string name)
    {
        var value = ParseDouble(cell, row, name);
        if (value <= 0)
        {
            throw new InvalidDataException($"Gun CSV row {row + 1}: column \"{name}\" must be positive.");
        }

        return value;
    }

    private static double UnitInterval(string cell, int row, string name)
    {
        var value = ParseDouble(cell, row, name);
        if (value < 0 || value > 1)
        {
            throw new InvalidDataException($"Gun CSV row {row + 1}: column \"{name}\" must be in [0, 1].");
        }

        return value;
    }

    private static double ParseDouble(string cell, int row, string name)
    {
        if (!double.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value))
        {
            throw new InvalidDataException($"Gun CSV row {row + 1}: column \"{name}\" is not a finite number.");
        }

        return value;
    }

    private static string Require(string cell, int row, string name)
    {
        var value = cell.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidDataException($"Gun CSV row {row + 1}: column \"{name}\" is empty.");
        }

        return value;
    }

    private static void RequireHeader(IReadOnlyList<string> lines)
    {
        if (lines.Count < 2 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Gun CSV header mismatch or no data rows.");
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
