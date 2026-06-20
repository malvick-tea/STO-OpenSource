using System;
using System.Collections.Generic;
using System.IO;
using Garupan.Content.Validation;

namespace Garupan.Content;

/// <summary>
/// Parses the tank-audio catalogue CSV (<c>data/tank-audio.csv</c>) into per-tank
/// <see cref="TankAudioProfile"/> records keyed by tank id. Per the no-hardcode rule the
/// sound-file paths are authoring data: a vehicle's audio identity is one CSV row of
/// <c>res://</c> paths, not C# string constants, so swapping or adding a tank's sounds is a
/// data edit with no recompile of the renderer.
/// </summary>
/// <remarks>
/// Schema (12 columns): <c>tank_id,engine_start,engine_stop,engine_rev_up,engine_rev_down,
/// engine_idle,engine_high,tracks,ground_effect,turret,gun,reload,is_default</c>. The eleven path
/// columns and default-profile marker map onto the <see cref="TankAudioProfile"/> constructor.
/// Validation is strict — a wrong column count, an empty cell, a duplicate tank id, or an
/// ambiguous default profile surfaces at load with row context.
/// Paths are not probed here (existence is a runtime concern of the SFX player).
/// </remarks>
public static class TankAudioProfileCsv
{
    private const string Header =
        "tank_id,engine_start,engine_stop,engine_rev_up,engine_rev_down,engine_idle,engine_high," +
        "tracks,ground_effect,turret,gun,reload,is_default";

    private const int ColumnCount = 13;

    public static IReadOnlyDictionary<string, TankAudioProfile> LoadFile(string csvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Tank-audio CSV not found: {csvPath}", csvPath);
        }

        return Parse(File.ReadAllText(csvPath));
    }

    public static IReadOnlyDictionary<string, TankAudioProfile> Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        if (lines.Count < 2)
        {
            throw new InvalidDataException("Tank-audio CSV must have at least a header row and one data row.");
        }

        if (!string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Tank-audio CSV header mismatch — expected \"{Header}\", got \"{lines[0]}\".");
        }

        var byId = new Dictionary<string, TankAudioProfile>(StringComparer.Ordinal);
        for (var row = 1; row < lines.Count; row++)
        {
            var (id, profile) = ParseRow(lines[row], row);
            if (!byId.TryAdd(id, profile))
            {
                throw new InvalidDataException(
                    $"Tank-audio CSV row {row + 1}: tank id \"{id}\" appears more than once.");
            }
        }

        var defaultCount = 0;
        foreach (var profile in byId.Values)
        {
            if (profile.IsDefault)
            {
                defaultCount++;
            }
        }

        if (defaultCount != 1)
        {
            throw new InvalidDataException(
                $"Tank-audio CSV must mark exactly one profile as default; found {defaultCount}.");
        }

        return byId;
    }

    private static (string Id, TankAudioProfile Profile) ParseRow(string line, int row)
    {
        var cells = line.Split(',');
        if (cells.Length != ColumnCount)
        {
            throw new InvalidDataException(
                $"Tank-audio CSV row {row + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        var id = RequireNonEmpty(cells[0], row, "tank_id");
        var profile = new TankAudioProfile(
            EngineStartPath: ContentResourcePath.Require(cells[1], row, "engine_start"),
            EngineStopPath: ContentResourcePath.Require(cells[2], row, "engine_stop"),
            EngineRevUpPath: ContentResourcePath.Require(cells[3], row, "engine_rev_up"),
            EngineRevDownPath: ContentResourcePath.Require(cells[4], row, "engine_rev_down"),
            EngineIdlePath: ContentResourcePath.Require(cells[5], row, "engine_idle"),
            EngineHighPath: ContentResourcePath.Require(cells[6], row, "engine_high"),
            TracksPath: ContentResourcePath.Require(cells[7], row, "tracks"),
            GroundEffectPath: ContentResourcePath.Require(cells[8], row, "ground_effect"),
            TurretPath: ContentResourcePath.Require(cells[9], row, "turret"),
            GunPath: ContentResourcePath.Require(cells[10], row, "gun"),
            ReloadPath: ContentResourcePath.Require(cells[11], row, "reload"),
            IsDefault: RequireBoolean(cells[12], row, "is_default"));
        return (id, profile);
    }

    private static bool RequireBoolean(string cell, int row, string columnName)
    {
        if (!bool.TryParse(cell.Trim(), out var value))
        {
            throw new InvalidDataException(
                $"Tank-audio CSV row {row + 1}: column \"{columnName}\" must be true or false.");
        }

        return value;
    }

    private static string RequireNonEmpty(string cell, int row, string columnName)
    {
        var trimmed = cell.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new InvalidDataException(
                $"Tank-audio CSV row {row + 1}: column \"{columnName}\" is empty.");
        }

        return trimmed;
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
