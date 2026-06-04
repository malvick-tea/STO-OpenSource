using System;
using System.Collections.Generic;
using System.IO;

namespace Garupan.Content;

/// <summary>Parses the destructible-prop kind catalogue. Every enum value must have one row,
/// so adding a separator label without assigning material physics fails at content load.</summary>
public static class PropKindCsv
{
    private const string Header = "kind,behavior,material_id";
    private const int ColumnCount = 3;

    public static IReadOnlyDictionary<PropKind, PropArchetype> Parse(
        string csv,
        Func<string, PropMaterial> resolveMaterial)
    {
        ArgumentNullException.ThrowIfNull(csv);
        ArgumentNullException.ThrowIfNull(resolveMaterial);
        var lines = SplitLines(csv);
        if (lines.Count < 2)
        {
            throw new InvalidDataException("Prop-kind CSV must have a header and at least one data row.");
        }

        if (!string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Prop-kind CSV header mismatch — expected \"{Header}\", got \"{lines[0]}\".");
        }

        var archetypes = new Dictionary<PropKind, PropArchetype>();
        for (var row = 1; row < lines.Count; row++)
        {
            var (kind, archetype) = ParseRow(lines[row], row, resolveMaterial);
            if (!archetypes.TryAdd(kind, archetype))
            {
                throw new InvalidDataException($"Prop-kind CSV row {row + 1}: duplicate kind \"{kind}\".");
            }
        }

        foreach (var kind in Enum.GetValues<PropKind>())
        {
            if (!archetypes.ContainsKey(kind))
            {
                throw new InvalidDataException($"Prop-kind CSV has no row for \"{kind}\".");
            }
        }

        return archetypes;
    }

    private static (PropKind Kind, PropArchetype Archetype) ParseRow(
        string line,
        int row,
        Func<string, PropMaterial> resolveMaterial)
    {
        var cells = line.Split(',');
        if (cells.Length != ColumnCount)
        {
            throw new InvalidDataException($"Prop-kind CSV row {row + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        var kind = ParseEnum<PropKind>(cells[0], row, "kind");
        var behavior = ParseEnum<PropBehavior>(cells[1], row, "behavior");
        var materialId = cells[2].Trim();
        try
        {
            return (kind, new PropArchetype(behavior, resolveMaterial(materialId)));
        }
        catch (KeyNotFoundException ex)
        {
            throw new InvalidDataException(
                $"Prop-kind CSV row {row + 1}: unknown material id \"{materialId}\".",
                ex);
        }
    }

    private static TEnum ParseEnum<TEnum>(string cell, int row, string columnName)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(cell.Trim(), ignoreCase: true, out var value)
            ? value
            : throw new InvalidDataException($"Prop-kind CSV row {row + 1}: unknown {columnName} \"{cell.Trim()}\".");

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
