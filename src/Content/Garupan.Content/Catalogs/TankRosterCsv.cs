using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Garupan.Content.Validation;

namespace Garupan.Content;

/// <summary>
/// Parses the master vehicle roster CSV (<c>data/tanks.csv</c>) into <see cref="TankSpec"/>
/// records. Per the ">200 tanks, no hardcode" mandate the roster is authoring data: a new
/// chassis is one CSV row referencing a shared gun / mount / drivetrain envelope by id, with
/// no C# edit and no recompile of consumers.
/// </summary>
/// <remarks>
/// Schema (36 columns):
/// <c>id,designation,display_name_key,model_res_path,school,crew_size,gun_id,reload_seconds,
/// rounds_per_minute,penetration_mm,mount_id,drive_id,mass_tonnes,engine_hp,engine_torque_nm,
/// body_length,body_width,body_height,turret_traverse_deg,hull_front_mm,hull_front_slope,
/// hull_side_mm,hull_side_slope,hull_rear_mm,hull_rear_slope,turret_front_mm,turret_front_slope,
/// turret_side_mm,turret_side_slope,turret_rear_mm,turret_rear_slope,mantlet_mm,mantlet_slope,
/// roof_mm,roof_slope,rolling_resistance</c>. Each armour plate is a thickness plus a mounting slope in degrees
/// from vertical (0 = vertical plate, 60 = the medium tank E glacis, 90 = a horizontal roof); the sim
/// resolves effective line-of-sight thickness from the slope and the shot's obliquity.
/// <para>
/// <c>gun_id</c>/<c>mount_id</c>/<c>drive_id</c> resolve through
/// <see cref="GunCatalog.FindById"/> / <see cref="GunMountCatalog.FindById"/> /
/// <see cref="GroundDriveCatalog.FindById"/> so 200 chassis share ~10 gun envelopes instead
/// of re-typing ballistics per row. The three override columns
/// (<c>reload_seconds</c>/<c>rounds_per_minute</c>/<c>penetration_mm</c>) are optional —
/// an empty cell inherits the gun-catalogue value; a present cell applies a per-chassis
/// <c>with</c> override. Validation is strict: an unknown id, a duplicate row id, a bad
/// number, or a malformed school surfaces at load with row context.
/// </para>
/// </remarks>
public static class TankRosterCsv
{
    private const string Header =
        "id,designation,display_name_key,model_res_path,school,crew_size,gun_id,reload_seconds," +
        "rounds_per_minute,penetration_mm,mount_id,drive_id,mass_tonnes,engine_hp,engine_torque_nm," +
        "body_length,body_width,body_height,turret_traverse_deg,hull_front_mm,hull_front_slope," +
        "hull_side_mm,hull_side_slope,hull_rear_mm,hull_rear_slope,turret_front_mm,turret_front_slope," +
        "turret_side_mm,turret_side_slope,turret_rear_mm,turret_rear_slope,mantlet_mm,mantlet_slope," +
        "roof_mm,roof_slope,rolling_resistance";

    private const int ColumnCount = 36;

    public static IReadOnlyList<TankSpec> LoadFile(string csvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Tank roster CSV not found: {csvPath}", csvPath);
        }

        return Parse(File.ReadAllText(csvPath));
    }

    public static IReadOnlyList<TankSpec> Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var lines = SplitLines(csv);
        if (lines.Count < 2)
        {
            throw new InvalidDataException("Tank roster CSV must have at least a header row and one data row.");
        }

        if (!string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Tank roster CSV header mismatch — expected \"{Header}\", got \"{lines[0]}\".");
        }

        var tanks = new List<TankSpec>(lines.Count - 1);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (var row = 1; row < lines.Count; row++)
        {
            var tank = ParseRow(lines[row], row);
            if (!seenIds.Add(tank.Id))
            {
                throw new InvalidDataException(
                    $"Tank roster CSV row {row + 1}: tank id \"{tank.Id}\" appears more than once.");
            }

            tanks.Add(tank);
        }

        return tanks;
    }

    private static TankSpec ParseRow(string line, int row)
    {
        var cells = line.Split(',');
        if (cells.Length != ColumnCount)
        {
            throw new InvalidDataException(
                $"Tank roster CSV row {row + 1}: expected {ColumnCount} columns, got {cells.Length}.");
        }

        var modelPath = ContentResourcePath.Require(
            cells[3],
            row,
            "model_res_path");
        var gun = BuildGun(cells, row);
        var mount = ResolveById(GunMountCatalog.FindById, cells[10], row, "mount_id");
        var drive = ResolveById(GroundDriveCatalog.FindById, cells[11], row, "drive_id");

        return new TankSpec(
            Id: RequireNonEmpty(cells[0], row, "id"),
            Designation: RequireNonEmpty(cells[1], row, "designation"),
            DisplayNameKey: RequireNonEmpty(cells[2], row, "display_name_key"),
            ModelResPath: modelPath,
            Armor: BuildArmor(cells, row),
            Gun: gun,
            GunMount: mount,
            Mobility: new MobilitySpec(
                MassTonnes: ParseDouble(cells[12], row, "mass_tonnes"),
                EnginePowerHorsepower: ParseDouble(cells[13], row, "engine_hp"),
                EnginePeakTorqueNewtonMeters: ParseDouble(cells[14], row, "engine_torque_nm"),
                BodyLengthMeters: ParseDouble(cells[15], row, "body_length"),
                BodyWidthMeters: ParseDouble(cells[16], row, "body_width"),
                BodyHeightMeters: ParseDouble(cells[17], row, "body_height"),
                TurretTraverseDegPerSec: ParseInt(cells[18], row, "turret_traverse_deg"),
                Drive: drive,
                RollingResistanceCoefficient: ParseDouble(cells[35], row, "rolling_resistance")),
            CrewSize: ParseInt(cells[5], row, "crew_size"))
        {
            School = ParseSchool(cells[4], row),
        };
    }

    /// <summary>Resolves the row's gun envelope and layers the optional per-chassis
    /// reload / rate-of-fire / penetration overrides over it.</summary>
    private static GunSpec BuildGun(IReadOnlyList<string> cells, int row)
    {
        var gun = ResolveById(GunCatalog.FindById, cells[6], row, "gun_id");
        if (ParseOptionalDouble(cells[7], row, "reload_seconds") is { } reload)
        {
            gun = gun with { ReloadSeconds = reload };
        }

        if (ParseOptionalInt(cells[8], row, "rounds_per_minute") is { } roundsPerMinute)
        {
            gun = gun with { RoundsPerMinute = roundsPerMinute };
        }

        if (ParseOptionalInt(cells[9], row, "penetration_mm") is { } penetration)
        {
            gun = gun with { PenetrationMm = penetration };
        }

        return gun;
    }

    /// <summary>Builds the layered armour profile from the eight thickness/slope plate pairs
    /// at the tail of the row (columns 19–34).</summary>
    private static ArmorProfile BuildArmor(IReadOnlyList<string> cells, int row) => new(
        HullFront: ParsePlate(cells, 19, 20, row, "hull_front"),
        HullSide: ParsePlate(cells, 21, 22, row, "hull_side"),
        HullRear: ParsePlate(cells, 23, 24, row, "hull_rear"),
        TurretFront: ParsePlate(cells, 25, 26, row, "turret_front"),
        TurretSide: ParsePlate(cells, 27, 28, row, "turret_side"),
        TurretRear: ParsePlate(cells, 29, 30, row, "turret_rear"),
        Mantlet: ParsePlate(cells, 31, 32, row, "mantlet"),
        Roof: ParsePlate(cells, 33, 34, row, "roof"));

    private static ArmorPlate ParsePlate(
        IReadOnlyList<string> cells, int thicknessColumn, int slopeColumn, int row, string plateName) => new(
        ThicknessMm: ParseInt(cells[thicknessColumn], row, $"{plateName}_mm"),
        SlopeDegrees: ParseInt(cells[slopeColumn], row, $"{plateName}_slope"));

    private static TValue ResolveById<TValue>(
        Func<string, TValue?> resolver, string cell, int row, string columnName)
        where TValue : class
    {
        var id = RequireNonEmpty(cell, row, columnName);
        return resolver(id)
            ?? throw new InvalidDataException(
                $"Tank roster CSV row {row + 1}: column \"{columnName}\" references unknown id \"{id}\".");
    }

    private static OpponentSchool ParseSchool(string cell, int row)
    {
        if (!Enum.TryParse<OpponentSchool>(cell.Trim(), ignoreCase: true, out var school))
        {
            throw new InvalidDataException(
                $"Tank roster CSV row {row + 1}: unknown school \"{cell}\".");
        }

        return school;
    }

    private static int ParseInt(string cell, int row, string columnName)
    {
        if (!int.TryParse(cell.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidDataException(
                $"Tank roster CSV row {row + 1}: column \"{columnName}\" is not an integer (\"{cell}\").");
        }

        return value;
    }

    private static double ParseDouble(string cell, int row, string columnName)
    {
        if (!double.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidDataException(
                $"Tank roster CSV row {row + 1}: column \"{columnName}\" is not a number (\"{cell}\").");
        }

        return value;
    }

    private static int? ParseOptionalInt(string cell, int row, string columnName) =>
        string.IsNullOrWhiteSpace(cell) ? null : ParseInt(cell, row, columnName);

    private static double? ParseOptionalDouble(string cell, int row, string columnName) =>
        string.IsNullOrWhiteSpace(cell) ? null : ParseDouble(cell, row, columnName);

    private static string RequireNonEmpty(string cell, int row, string columnName)
    {
        var trimmed = cell.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new InvalidDataException(
                $"Tank roster CSV row {row + 1}: column \"{columnName}\" is empty.");
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
