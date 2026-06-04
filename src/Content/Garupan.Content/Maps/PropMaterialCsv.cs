using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Garupan.Content;

/// <summary>Parses the destructible-prop material catalogue. Physical constants stay in
/// <c>data/prop-materials.csv</c>, so balancing a source value or adding a material is a data
/// review rather than a simulation-code edit.</summary>
public static class PropMaterialCsv
{
    private const string Header = "id,modulus_of_rupture_pa,failure_deflection_radians,density_kg_per_cubic_meter";
    private const int ColumnCount = 4;

    public static IReadOnlyDictionary<string, PropMaterial> Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        if (lines.Count < 2)
        {
            throw new InvalidDataException("Prop-material CSV must have a header and at least one data row.");
        }

        if (!string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Prop-material CSV header mismatch — expected \"{Header}\", got \"{lines[0]}\".");
        }

        var materials = new Dictionary<string, PropMaterial>(StringComparer.Ordinal);
        for (var row = 1; row < lines.Count; row++)
        {
            var material = ParseRow(lines[row], row);
            if (!materials.TryAdd(material.Name, material))
            {
                throw new InvalidDataException($"Prop-material CSV row {row + 1}: duplicate id \"{material.Name}\".");
            }
        }

        return materials;
    }

    private static PropMaterial ParseRow(string line, int row)
    {
        var cells = line.Split(',');
        if (cells.Length != ColumnCount)
        {
            throw new InvalidDataException($"Prop-material CSV row {row + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        return new PropMaterial(
            RequireNonEmpty(cells[0], row, "id"),
            ParsePositive(cells[1], row, "modulus_of_rupture_pa"),
            ParsePositive(cells[2], row, "failure_deflection_radians"),
            ParsePositive(cells[3], row, "density_kg_per_cubic_meter"));
    }

    private static float ParsePositive(string cell, int row, string columnName)
    {
        if (!float.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !float.IsFinite(value)
            || value <= 0f)
        {
            throw new InvalidDataException(
                $"Prop-material CSV row {row + 1}: column \"{columnName}\" must be a finite positive number (\"{cell}\").");
        }

        return value;
    }

    private static string RequireNonEmpty(string cell, int row, string columnName)
    {
        var value = cell.Trim();
        return value.Length > 0
            ? value
            : throw new InvalidDataException($"Prop-material CSV row {row + 1}: column \"{columnName}\" is empty.");
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
