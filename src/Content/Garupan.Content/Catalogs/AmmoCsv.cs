using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Garupan.Content;

/// <summary>Strict parser for the authoring-side ammunition catalogue.</summary>
public static class AmmoCsv
{
    private const string Header =
        "id,type,muzzle_velocity_mps,mass_kg,penetration_mm,diameter_meters,drag_coefficient," +
        "propellant_charge_mass_kg,gas_velocity_factor";

    private const int ColumnCount = 9;

    public static IReadOnlyList<AmmoSpec> Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        RequireHeader(lines);
        var rounds = new List<AmmoSpec>(lines.Count - 1);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var row = 1; row < lines.Count; row++)
        {
            var round = ParseRow(lines[row], row);
            if (!ids.Add(round.Id))
            {
                throw new InvalidDataException($"Ammo CSV row {row + 1}: duplicate id \"{round.Id}\".");
            }

            rounds.Add(round);
        }

        return rounds;
    }

    private static AmmoSpec ParseRow(string line, int row)
    {
        var cells = line.Split(',');
        if (cells.Length != ColumnCount)
        {
            throw new InvalidDataException($"Ammo CSV row {row + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        return new AmmoSpec(
            Require(cells[0], row, "id"),
            ParseAmmoType(cells[1], row),
            Positive(cells[2], row, "muzzle_velocity_mps"),
            Positive(cells[3], row, "mass_kg"),
            NonNegative(cells[4], row, "penetration_mm"),
            Positive(cells[5], row, "diameter_meters"),
            NonNegative(cells[6], row, "drag_coefficient"),
            NonNegative(cells[7], row, "propellant_charge_mass_kg"),
            NonNegative(cells[8], row, "gas_velocity_factor"));
    }

    private static AmmoType ParseAmmoType(string cell, int row)
    {
        if (!Enum.TryParse<AmmoType>(cell.Trim(), ignoreCase: false, out var type))
        {
            throw new InvalidDataException($"Ammo CSV row {row + 1}: unknown type \"{cell}\".");
        }

        return type;
    }

    private static float Positive(string cell, int row, string name)
    {
        var value = ParseFloat(cell, row, name);
        if (value <= 0f)
        {
            throw new InvalidDataException($"Ammo CSV row {row + 1}: column \"{name}\" must be positive.");
        }

        return value;
    }

    private static float NonNegative(string cell, int row, string name)
    {
        var value = ParseFloat(cell, row, name);
        if (value < 0f)
        {
            throw new InvalidDataException($"Ammo CSV row {row + 1}: column \"{name}\" must be non-negative.");
        }

        return value;
    }

    private static float ParseFloat(string cell, int row, string name)
    {
        if (!float.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !float.IsFinite(value))
        {
            throw new InvalidDataException($"Ammo CSV row {row + 1}: column \"{name}\" is not a finite number.");
        }

        return value;
    }

    private static string Require(string cell, int row, string name)
    {
        var value = cell.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidDataException($"Ammo CSV row {row + 1}: column \"{name}\" is empty.");
        }

        return value;
    }

    private static void RequireHeader(IReadOnlyList<string> lines)
    {
        if (lines.Count < 2 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Ammo CSV header mismatch or no data rows.");
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
