using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Opus.Content;

namespace Garupan.Content;

/// <summary>
/// Parses scene lighting CSV files (<c>data/*-lighting.csv</c>) into a
/// <see cref="LightingPreset"/>. Schema: header row + four data rows keyed by
/// <c>sun_direction</c> / <c>sun_colour</c> / <c>ambient_colour</c> / <c>horizon_colour</c>;
/// columns <c>key,r,g,b,canon_source</c>.
/// <c>sun_direction</c> is normalised by the loader so the renderer can multiply the
/// stored vector directly.
/// </summary>
public static class LightingPresetCsv
{
    private const string Header = "key,r,g,b,canon_source";
    private const int ColumnCount = 5;

    private const string KeySunDirection = "sun_direction";
    private const string KeySunColour = "sun_colour";
    private const string KeyAmbientColour = "ambient_colour";
    private const string KeyHorizonColour = "horizon_colour";

    public static LightingPreset LoadFile(string csvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Lighting preset CSV not found: {csvPath}", csvPath);
        }

        return Parse(File.ReadAllText(csvPath));
    }

    public static LightingPreset Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        if (lines.Count < 2)
        {
            throw new InvalidDataException("Lighting preset CSV must have at least a header row and one data row.");
        }

        if (!string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Lighting preset CSV header mismatch — expected \"{Header}\", got \"{lines[0]}\".");
        }

        var values = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        for (var row = 1; row < lines.Count; row++)
        {
            var (key, value) = ParseRow(lines[row], row);
            if (values.ContainsKey(key))
            {
                throw new InvalidDataException(
                    $"Lighting preset CSV row {row + 1}: key \"{key}\" appears more than once.");
            }

            values[key] = value;
        }

        var sunDirRaw = RequireKey(values, KeySunDirection);
        if (sunDirRaw.LengthSquared() <= 0f)
        {
            throw new InvalidDataException($"Lighting preset CSV: \"{KeySunDirection}\" must be a non-zero vector.");
        }

        return new LightingPreset(
            SunDirection: Vector3.Normalize(sunDirRaw),
            SunColour: RequireKey(values, KeySunColour),
            AmbientColour: RequireKey(values, KeyAmbientColour),
            HorizonColour: RequireKey(values, KeyHorizonColour));
    }

    private static (string Key, Vector3 Value) ParseRow(string line, int rowIndex)
    {
        var cells = line.Split(',', ColumnCount);
        if (cells.Length < ColumnCount)
        {
            throw new InvalidDataException(
                $"Lighting preset CSV row {rowIndex + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        var key = cells[0].Trim();
        var r = ParseChannel(cells[1], rowIndex, "r");
        var g = ParseChannel(cells[2], rowIndex, "g");
        var b = ParseChannel(cells[3], rowIndex, "b");
        return (key, new Vector3(r, g, b));
    }

    private static float ParseChannel(string cell, int rowIndex, string columnName)
    {
        if (!float.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidDataException(
                $"Lighting preset CSV row {rowIndex + 1}: column \"{columnName}\" is not a valid float (\"{cell}\").");
        }

        if (!float.IsFinite(value))
        {
            throw new InvalidDataException(
                $"Lighting preset CSV row {rowIndex + 1}: column \"{columnName}\" must be finite (got {value}).");
        }

        return value;
    }

    private static Vector3 RequireKey(Dictionary<string, Vector3> values, string key)
    {
        if (!values.TryGetValue(key, out var value))
        {
            throw new InvalidDataException(
                $"Lighting preset CSV: required key \"{key}\" missing.");
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
