using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace Garupan.Content;

/// <summary>
/// Parses <c>data/school-palette.csv</c> into a <see cref="SchoolPalette"/> instance.
/// Canon paint factors live in the CSV (one row per <see cref="OpponentSchool"/> with
/// <c>r,g,b,a</c> floats + a <c>canon_source</c> attribution column) so artists /
/// loremasters can tune them without recompiling C#. The schema is deliberately tiny:
/// header row + N data rows, comma-separated, no quotes / escapes needed since every
/// cell except <c>canon_source</c> is a numeric or short identifier.
/// </summary>
/// <remarks>
/// Validation is strict — a missing column, an unknown school name, or a non-finite
/// number throws <see cref="InvalidDataException"/> with a row + column hint. Test
/// fixtures rely on a well-formed CSV; in runtime this is loaded once at startup so
/// any error surfaces immediately, not deep into a match.
/// </remarks>
public static class SchoolPaletteCsv
{
    private const string Header = "school,r,g,b,a,canon_source";
    private const int ColumnCount = 6;

    public static SchoolPalette LoadFile(string csvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"School palette CSV not found: {csvPath}", csvPath);
        }

        return Parse(File.ReadAllText(csvPath));
    }

    public static SchoolPalette Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        if (lines.Count < 2)
        {
            throw new InvalidDataException("School palette CSV must have at least a header row and one data row.");
        }

        if (!string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"School palette CSV header mismatch — expected \"{Header}\", got \"{lines[0]}\".");
        }

        var entries = new Dictionary<OpponentSchool, Vector4>();
        for (var row = 1; row < lines.Count; row++)
        {
            var (school, tint) = ParseRow(lines[row], row);
            if (entries.ContainsKey(school))
            {
                throw new InvalidDataException(
                    $"School palette CSV row {row + 1}: school \"{school}\" appears more than once.");
            }

            entries[school] = tint;
        }

        return new SchoolPalette(entries);
    }

    private static (OpponentSchool School, Vector4 Tint) ParseRow(string line, int rowIndex)
    {
        var cells = line.Split(',', ColumnCount);
        if (cells.Length < 5)
        {
            throw new InvalidDataException(
                $"School palette CSV row {rowIndex + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        var schoolName = cells[0].Trim();
        var school = ParseSchool(schoolName, rowIndex);
        var r = ParseChannel(cells[1], rowIndex, "r");
        var g = ParseChannel(cells[2], rowIndex, "g");
        var b = ParseChannel(cells[3], rowIndex, "b");
        var a = ParseChannel(cells[4], rowIndex, "a");
        return (school, new Vector4(r, g, b, a));
    }

    private static OpponentSchool ParseSchool(string name, int rowIndex)
    {
        var normalized = name.Replace("_", string.Empty, StringComparison.Ordinal);
        if (!Enum.TryParse<OpponentSchool>(
                normalized,
                ignoreCase: true,
                out var parsed))
        {
            throw new InvalidDataException(
                $"School palette CSV row {rowIndex + 1}: unknown school \"{name}\".");
        }

        return parsed;
    }

    private static float ParseChannel(string cell, int rowIndex, string columnName)
    {
        if (!float.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidDataException(
                $"School palette CSV row {rowIndex + 1}: column \"{columnName}\" is not a valid float (\"{cell}\").");
        }

        if (!float.IsFinite(value))
        {
            throw new InvalidDataException(
                $"School palette CSV row {rowIndex + 1}: column \"{columnName}\" must be finite (got {value}).");
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
