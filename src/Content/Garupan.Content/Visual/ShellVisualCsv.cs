using System;
using System.Collections.Generic;
using System.IO;
using Garupan.Content.Validation;

namespace Garupan.Content;

/// <summary>
/// Parses <c>data/shell-visuals.csv</c> into a <see cref="ShellVisualCatalog"/>. Per
/// ADR-0030, shell models bound to ammo families are authoring data — artists ship a
/// glTF asset and add one row.
/// </summary>
/// <remarks>
/// Schema: <c>ammo_type,model_vfs_path,canon_source</c>. The <c>canon_source</c>
/// column (last) attributes the model — Sketchfab author / asset library / in-house
/// scan — and may contain commas; the parser uses <c>string.Split(',', N)</c> so
/// trailing commas in the source line stay in the source column.
/// </remarks>
public static class ShellVisualCsv
{
    private const string Header = "ammo_type,model_vfs_path,canon_source";
    private const int ColumnCount = 3;

    public static ShellVisualCatalog LoadFile(string csvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Shell visual CSV not found: {csvPath}", csvPath);
        }

        return Parse(File.ReadAllText(csvPath));
    }

    public static ShellVisualCatalog Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        if (lines.Count < 2)
        {
            throw new InvalidDataException("Shell visual CSV must have at least a header row and one data row.");
        }

        if (!string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Shell visual CSV header mismatch — expected \"{Header}\", got \"{lines[0]}\".");
        }

        var entries = new Dictionary<AmmoType, ShellVisualSpec>();
        for (var row = 1; row < lines.Count; row++)
        {
            var spec = ParseRow(lines[row], row);
            if (entries.ContainsKey(spec.AmmoType))
            {
                throw new InvalidDataException(
                    $"Shell visual CSV row {row + 1}: ammo type \"{spec.AmmoType}\" appears more than once.");
            }

            entries[spec.AmmoType] = spec;
        }

        return new ShellVisualCatalog(entries);
    }

    private static ShellVisualSpec ParseRow(string line, int rowIndex)
    {
        var cells = line.Split(',', ColumnCount);
        if (cells.Length < ColumnCount - 1)
        {
            throw new InvalidDataException(
                $"Shell visual CSV row {rowIndex + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        var ammoStr = cells[0].Trim();
        if (!Enum.TryParse<AmmoType>(ammoStr, ignoreCase: true, out var ammo))
        {
            throw new InvalidDataException(
                $"Shell visual CSV row {rowIndex + 1}: unknown ammo type \"{ammoStr}\" (expected AP / APCR / HEAT / HE).");
        }

        var path = ContentResourcePath.Require(
            cells[1],
            rowIndex,
            "model_vfs_path");

        return new ShellVisualSpec(ammo, path);
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
