using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Garupan.Content;

/// <summary>
/// Parses <c>data/ai-personalities.csv</c> into a <see cref="BotPersonalityCatalog"/>.
/// Per ADR-0030, per-school AI tuning is authoring data — designers + balancers iterate
/// it without touching C#. Schema: <c>school,engage_range_m,throttle_scale,alignment_tolerance_radians,canon_source</c>.
/// </summary>
/// <remarks>
/// Validation is strict and column-named: a missing column, an unknown school, a
/// non-finite number, or a value outside [0..1] for throttle / [0..π] for alignment
/// throws <see cref="InvalidDataException"/> with row + column hints so the boot stage
/// surfaces broken data immediately.
/// </remarks>
public static class BotPersonalityCsv
{
    private const string Header = "school,engage_range_m,throttle_scale,alignment_tolerance_radians,canon_source";
    private const int ColumnCount = 5;

    public static BotPersonalityCatalog LoadFile(string csvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Bot personality CSV not found: {csvPath}", csvPath);
        }

        return Parse(File.ReadAllText(csvPath));
    }

    public static BotPersonalityCatalog Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        if (lines.Count < 2)
        {
            throw new InvalidDataException("Bot personality CSV must have at least a header row and one data row.");
        }

        if (!string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Bot personality CSV header mismatch — expected \"{Header}\", got \"{lines[0]}\".");
        }

        var entries = new Dictionary<OpponentSchool, BotPersonality>();
        for (var row = 1; row < lines.Count; row++)
        {
            var personality = ParseRow(lines[row], row);
            if (entries.ContainsKey(personality.School))
            {
                throw new InvalidDataException(
                    $"Bot personality CSV row {row + 1}: school \"{personality.School}\" appears more than once.");
            }

            entries[personality.School] = personality;
        }

        return new BotPersonalityCatalog(entries);
    }

    private static BotPersonality ParseRow(string line, int rowIndex)
    {
        var cells = line.Split(',', ColumnCount);
        if (cells.Length < ColumnCount - 1)
        {
            throw new InvalidDataException(
                $"Bot personality CSV row {rowIndex + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        var school = ParseSchool(cells[0].Trim(), rowIndex);
        var engageRange = ParseFloat(cells[1], rowIndex, "engage_range_m");
        var throttleScale = ParseFloat(cells[2], rowIndex, "throttle_scale");
        var alignmentTolerance = ParseFloat(cells[3], rowIndex, "alignment_tolerance_radians");

        try
        {
            return BotPersonality.CreateValidated(school, engageRange, throttleScale, alignmentTolerance);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new InvalidDataException(
                $"Bot personality CSV row {rowIndex + 1}: {ex.Message}", ex);
        }
    }

    private static OpponentSchool ParseSchool(string name, int rowIndex)
    {
        if (!Enum.TryParse<OpponentSchool>(name, ignoreCase: true, out var parsed))
        {
            throw new InvalidDataException(
                $"Bot personality CSV row {rowIndex + 1}: unknown school \"{name}\".");
        }

        return parsed;
    }

    private static float ParseFloat(string cell, int rowIndex, string columnName)
    {
        if (!float.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidDataException(
                $"Bot personality CSV row {rowIndex + 1}: column \"{columnName}\" is not a valid float (\"{cell}\").");
        }

        if (!float.IsFinite(value))
        {
            throw new InvalidDataException(
                $"Bot personality CSV row {rowIndex + 1}: column \"{columnName}\" must be finite (got {value}).");
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
