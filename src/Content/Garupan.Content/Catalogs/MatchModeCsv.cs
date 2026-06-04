using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Garupan.Content;

/// <summary>
/// Parses <c>data/match-modes.csv</c> into a <see cref="MatchModeCatalog"/>. Schema:
/// <c>id,kind,name_key,summary_key,lobby_capacity,respawn_limit,commander_led</c>.
/// Validation is strict — a missing column, unknown kind, non-positive capacity, or
/// duplicate id throws <see cref="InvalidDataException"/> with a row number hint so the
/// boot stage can surface the failure before the lobby ever opens.
/// </summary>
public static class MatchModeCsv
{
    private const string Header = "id,kind,name_key,summary_key,lobby_capacity,respawn_limit,commander_led";
    private const int ColumnCount = 7;

    public static MatchModeCatalog LoadFile(string csvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Match mode CSV not found: {csvPath}", csvPath);
        }

        return Parse(File.ReadAllText(csvPath));
    }

    public static MatchModeCatalog Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitNonEmptyLines(csv);
        if (lines.Count < 2)
        {
            throw new InvalidDataException(
                "Match mode CSV must have at least a header row and one data row.");
        }

        if (!string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Match mode CSV header mismatch — expected \"{Header}\", got \"{lines[0]}\".");
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var modes = new List<MatchMode>(lines.Count - 1);
        for (var row = 1; row < lines.Count; row++)
        {
            var mode = ParseRow(lines[row], row);
            if (!seenIds.Add(mode.Id))
            {
                throw new InvalidDataException(
                    $"Match mode CSV row {row + 1}: duplicate id \"{mode.Id}\".");
            }

            modes.Add(mode);
        }

        return new MatchModeCatalog(modes);
    }

    private static MatchMode ParseRow(string line, int rowIndex)
    {
        var cells = line.Split(',', ColumnCount);
        if (cells.Length != ColumnCount)
        {
            throw new InvalidDataException(
                $"Match mode CSV row {rowIndex + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        var id = cells[0].Trim();
        var kind = ParseKind(cells[1].Trim(), rowIndex);
        var nameKey = cells[2].Trim();
        var summaryKey = cells[3].Trim();
        var lobbyCapacity = ParseInt(cells[4], rowIndex, "lobby_capacity");
        var respawnLimit = ParseInt(cells[5], rowIndex, "respawn_limit");
        var commanderLed = ParseBool(cells[6], rowIndex, "commander_led");

        try
        {
            return MatchMode.CreateValidated(
                id, kind, nameKey, summaryKey, lobbyCapacity, respawnLimit, commanderLed);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException(
                $"Match mode CSV row {rowIndex + 1}: {ex.Message}", ex);
        }
    }

    private static MatchModeKind ParseKind(string cell, int rowIndex)
    {
        if (!Enum.TryParse<MatchModeKind>(cell, ignoreCase: true, out var parsed))
        {
            throw new InvalidDataException(
                $"Match mode CSV row {rowIndex + 1}: unknown kind \"{cell}\".");
        }

        return parsed;
    }

    private static int ParseInt(string cell, int rowIndex, string columnName)
    {
        if (!int.TryParse(cell.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidDataException(
                $"Match mode CSV row {rowIndex + 1}: column \"{columnName}\" is not an integer (\"{cell}\").");
        }

        return value;
    }

    private static bool ParseBool(string cell, int rowIndex, string columnName)
    {
        var trimmed = cell.Trim();
        if (bool.TryParse(trimmed, out var value))
        {
            return value;
        }

        throw new InvalidDataException(
            $"Match mode CSV row {rowIndex + 1}: column \"{columnName}\" must be true|false (\"{cell}\").");
    }

    private static List<string> SplitNonEmptyLines(string csv)
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
