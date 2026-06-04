using System;
using System.Collections.Generic;
using System.IO;

namespace Garupan.Content;

/// <summary>
/// Parses one crew-roster CSV (e.g. <c>data/crews/player_crew.csv</c>) into a
/// <see cref="CrewRoster"/>. Per ADR-0030, the canon roster is authoring data — a
/// writer / loremaster adds a new team member via one CSV row, no C# edit.
/// </summary>
/// <remarks>
/// Schema: <c>id,given_name,family_name,role,role_key,school_key</c>. Six columns; the
/// loader verifies every <c>school_key</c> matches across rows so a roster doesn't
/// silently mix schools. Validation is strict; broken data surfaces at boot.
/// </remarks>
public static class CrewRosterCsv
{
    private const string Header = "id,given_name,family_name,role,role_key,school_key";
    private const int ColumnCount = 6;

    public static CrewRoster LoadFile(string csvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Crew roster CSV not found: {csvPath}", csvPath);
        }

        return Parse(File.ReadAllText(csvPath));
    }

    public static CrewRoster Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        if (lines.Count < 2)
        {
            throw new InvalidDataException("Crew roster CSV must have at least a header row and one data row.");
        }

        if (!string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Crew roster CSV header mismatch — expected \"{Header}\", got \"{lines[0]}\".");
        }

        var members = new List<CrewMember>(lines.Count - 1);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        string? rosterSchoolKey = null;

        for (var row = 1; row < lines.Count; row++)
        {
            var member = ParseRow(lines[row], row);
            if (!seenIds.Add(member.Id))
            {
                throw new InvalidDataException(
                    $"Crew roster CSV row {row + 1}: member id \"{member.Id}\" appears more than once.");
            }

            rosterSchoolKey ??= member.SchoolKey;
            if (!string.Equals(rosterSchoolKey, member.SchoolKey, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Crew roster CSV row {row + 1}: member \"{member.Id}\" has school_key \"{member.SchoolKey}\" but earlier rows use \"{rosterSchoolKey}\". One CSV = one school.");
            }

            members.Add(member);
        }

        return new CrewRoster(members, rosterSchoolKey!);
    }

    private static CrewMember ParseRow(string line, int rowIndex)
    {
        var cells = line.Split(',', ColumnCount);
        if (cells.Length < ColumnCount)
        {
            throw new InvalidDataException(
                $"Crew roster CSV row {rowIndex + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        var id = RequireNonEmpty(cells[0], rowIndex, "id");
        var givenName = RequireNonEmpty(cells[1], rowIndex, "given_name");
        var familyName = RequireNonEmpty(cells[2], rowIndex, "family_name");
        var role = ParseRole(cells[3], rowIndex);
        var roleKey = RequireNonEmpty(cells[4], rowIndex, "role_key");
        var schoolKey = RequireNonEmpty(cells[5], rowIndex, "school_key");
        return new CrewMember(id, givenName, familyName, role, roleKey, schoolKey);
    }

    private static string RequireNonEmpty(string cell, int rowIndex, string columnName)
    {
        var trimmed = cell.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new InvalidDataException(
                $"Crew roster CSV row {rowIndex + 1}: column \"{columnName}\" is empty.");
        }

        return trimmed;
    }

    private static CrewRole ParseRole(string cell, int rowIndex)
    {
        if (!Enum.TryParse<CrewRole>(cell.Trim(), ignoreCase: true, out var parsed))
        {
            throw new InvalidDataException(
                $"Crew roster CSV row {rowIndex + 1}: unknown role \"{cell}\" (expected Commander / Gunner / Loader / Driver / RadioOperator).");
        }

        return parsed;
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
