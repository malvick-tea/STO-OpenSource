using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Garupan.Content;

/// <summary>
/// Parses one campaign-CSV (e.g. <c>data/campaigns/sample.csv</c>) into a
/// <see cref="CampaignSpec"/>. Per ADR-0030, mission metadata + graph layout are
/// authoring data — writers / designers iterate them without recompiling.
/// </summary>
/// <remarks>
/// <para>
/// Schema: <c>id,title_key,episode,opponent,environment,objective,lore_key,
/// briefing_key,script_id,node_x,node_y,prerequisites</c>. Twelve columns; the last
/// (<c>prerequisites</c>) is a pipe-separated (<c>|</c>) list of mission ids,
/// possibly empty.
/// </para>
/// <para>
/// Campaign-level identity (id / name_key / short_description_key) is supplied by the
/// caller, not the CSV — different campaigns share the same row schema and only
/// differ at the meta level. The loader cross-validates every prerequisite id against
/// the mission ids in the same CSV so a typo surfaces at boot.
/// </para>
/// </remarks>
public static class CampaignSpecCsv
{
    private const string Header = "id,title_key,episode,opponent,environment,objective,lore_key,briefing_key,script_id,node_x,node_y,prerequisites";
    private const int ColumnCount = 12;
    private const char PrerequisiteSeparator = '|';

    public static CampaignSpec LoadFile(
        string csvPath,
        string campaignId,
        string nameKey,
        string shortDescriptionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Campaign CSV not found: {csvPath}", csvPath);
        }

        return Parse(File.ReadAllText(csvPath), campaignId, nameKey, shortDescriptionKey);
    }

    public static CampaignSpec Parse(
        string csv,
        string campaignId,
        string nameKey,
        string shortDescriptionKey)
    {
        ArgumentNullException.ThrowIfNull(csv);
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(shortDescriptionKey);

        var lines = SplitLines(csv);
        EnsureHeader(lines);

        var (missions, nodes) = ParseRows(lines);
        ValidatePrerequisites(missions, nodes);

        return new CampaignSpec(campaignId, nameKey, shortDescriptionKey, missions, nodes);
    }

    private static void EnsureHeader(List<string> lines)
    {
        if (lines.Count < 2)
        {
            throw new InvalidDataException("Campaign CSV must have at least a header row and one data row.");
        }

        if (!string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Campaign CSV header mismatch — expected \"{Header}\", got \"{lines[0]}\".");
        }
    }

    private static (IReadOnlyList<MissionSpec> Missions, IReadOnlyList<CampaignNode> Nodes) ParseRows(List<string> lines)
    {
        var missions = new List<MissionSpec>(lines.Count - 1);
        var nodes = new List<CampaignNode>(lines.Count - 1);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (var row = 1; row < lines.Count; row++)
        {
            var (mission, node) = ParseRow(lines[row], row);
            if (!seenIds.Add(mission.Id))
            {
                throw new InvalidDataException(
                    $"Campaign CSV row {row + 1}: mission id \"{mission.Id}\" appears more than once.");
            }

            missions.Add(mission);
            nodes.Add(node);
        }

        return (missions, nodes);
    }

    private static (MissionSpec Mission, CampaignNode Node) ParseRow(string line, int rowIndex)
    {
        var cells = line.Split(',', ColumnCount);
        if (cells.Length < ColumnCount)
        {
            throw new InvalidDataException(
                $"Campaign CSV row {rowIndex + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        var id = RequireNonEmpty(cells[0], rowIndex, "id");
        var titleKey = RequireNonEmpty(cells[1], rowIndex, "title_key");
        var episode = RequireNonEmpty(cells[2], rowIndex, "episode");
        var opponent = ParseEnum<OpponentSchool>(cells[3], rowIndex, "opponent");
        var environment = ParseEnum<MissionEnvironment>(cells[4], rowIndex, "environment");
        var objective = ParseEnum<MissionObjective>(cells[5], rowIndex, "objective");
        var loreKey = RequireNonEmpty(cells[6], rowIndex, "lore_key");
        var briefingKey = RequireNonEmpty(cells[7], rowIndex, "briefing_key");
        var scriptId = RequireNonEmpty(cells[8], rowIndex, "script_id");
        var nodeX = ParseFloat(cells[9], rowIndex, "node_x");
        var nodeY = ParseFloat(cells[10], rowIndex, "node_y");
        var prerequisites = ParsePrerequisites(cells[11]);

        var mission = MissionSpec.Of(id, titleKey, episode, opponent, environment, objective, loreKey, briefingKey, scriptId);
        var node = new CampaignNode(id, nodeX, nodeY, prerequisites);
        return (mission, node);
    }

    private static string RequireNonEmpty(string cell, int rowIndex, string columnName)
    {
        var trimmed = cell.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new InvalidDataException(
                $"Campaign CSV row {rowIndex + 1}: column \"{columnName}\" is empty.");
        }

        return trimmed;
    }

    private static TEnum ParseEnum<TEnum>(string cell, int rowIndex, string columnName)
        where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(cell.Trim(), ignoreCase: true, out var parsed))
        {
            throw new InvalidDataException(
                $"Campaign CSV row {rowIndex + 1}: column \"{columnName}\" value \"{cell}\" is not a valid {typeof(TEnum).Name}.");
        }

        return parsed;
    }

    private static float ParseFloat(string cell, int rowIndex, string columnName)
    {
        if (!float.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidDataException(
                $"Campaign CSV row {rowIndex + 1}: column \"{columnName}\" is not a valid float (\"{cell}\").");
        }

        if (!float.IsFinite(value))
        {
            throw new InvalidDataException(
                $"Campaign CSV row {rowIndex + 1}: column \"{columnName}\" must be finite (got {value}).");
        }

        return value;
    }

    private static IReadOnlyList<string> ParsePrerequisites(string cell)
    {
        var trimmed = cell.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return Array.Empty<string>();
        }

        var raw = trimmed.Split(PrerequisiteSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (raw.Length == 0)
        {
            return Array.Empty<string>();
        }

        return raw;
    }

    private static void ValidatePrerequisites(
        IReadOnlyList<MissionSpec> missions,
        IReadOnlyList<CampaignNode> nodes)
    {
        var missionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mission in missions)
        {
            missionIds.Add(mission.Id);
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            foreach (var prereq in node.Prerequisites)
            {
                if (!missionIds.Contains(prereq))
                {
                    throw new InvalidDataException(
                        $"Campaign CSV row {i + 2}: prerequisite \"{prereq}\" for mission \"{node.MissionId}\" does not match any mission id in this campaign.");
                }

                if (string.Equals(prereq, node.MissionId, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Campaign CSV row {i + 2}: mission \"{node.MissionId}\" lists itself as a prerequisite.");
                }
            }
        }
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
