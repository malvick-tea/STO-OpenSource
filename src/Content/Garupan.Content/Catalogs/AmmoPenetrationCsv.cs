using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Garupan.Content;

/// <summary>
/// Strict parser for the per-round penetration tables (<c>data/ammo-penetration.csv</c>).
/// </summary>
/// <remarks>
/// Schema (4 columns): <c>ammo_id,normal_100m_mm,normal_500m_mm,normal_1000m_mm</c>. Each value
/// is the perpendicular (0° obliquity) penetration of homogeneous rolled armour in millimetres at
/// that range; the resolver applies plate slope and shot azimuth geometrically, so no angle column
/// is stored. Figures are the published service-acceptance values where available (German
/// the AP shell/40 tables, Soviet BR-365, US M62/M93 / HVAP M93), best-effort otherwise; a shaped-charge
/// (HEAT) round is flat versus range, and an HE round carries a blast-equivalent placeholder pending
/// a dedicated explosive-effect model. Validation is strict — a wrong column count, a non-finite or
/// negative value, or a duplicate ammo id surfaces at load with row context.
/// </remarks>
public static class AmmoPenetrationCsv
{
    private const string Header = "ammo_id,normal_100m_mm,normal_500m_mm,normal_1000m_mm";

    private const int ColumnCount = 4;

    public static IReadOnlyList<PenetrationCurve> Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        RequireHeader(lines);
        var curves = new List<PenetrationCurve>(lines.Count - 1);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var row = 1; row < lines.Count; row++)
        {
            var curve = ParseRow(lines[row], row);
            if (!ids.Add(curve.AmmoId))
            {
                throw new InvalidDataException($"Ammo-penetration CSV row {row + 1}: duplicate ammo id \"{curve.AmmoId}\".");
            }

            curves.Add(curve);
        }

        return curves;
    }

    private static PenetrationCurve ParseRow(string line, int row)
    {
        var cells = line.Split(',');
        if (cells.Length != ColumnCount)
        {
            throw new InvalidDataException(
                $"Ammo-penetration CSV row {row + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        return new PenetrationCurve(
            Require(cells[0], row, "ammo_id"),
            NonNegative(cells[1], row, "normal_100m_mm"),
            NonNegative(cells[2], row, "normal_500m_mm"),
            NonNegative(cells[3], row, "normal_1000m_mm"));
    }

    private static float NonNegative(string cell, int row, string name)
    {
        if (!float.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !float.IsFinite(value) || value < 0f)
        {
            throw new InvalidDataException(
                $"Ammo-penetration CSV row {row + 1}: column \"{name}\" must be a finite non-negative number.");
        }

        return value;
    }

    private static string Require(string cell, int row, string name)
    {
        var value = cell.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidDataException($"Ammo-penetration CSV row {row + 1}: column \"{name}\" is empty.");
        }

        return value;
    }

    private static void RequireHeader(IReadOnlyList<string> lines)
    {
        if (lines.Count < 2 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Ammo-penetration CSV header mismatch or no data rows.");
        }
    }

    private static List<string> SplitLines(string csv)
    {
        var lines = new List<string>();
        using var reader = new StringReader(csv);
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        return lines;
    }
}
