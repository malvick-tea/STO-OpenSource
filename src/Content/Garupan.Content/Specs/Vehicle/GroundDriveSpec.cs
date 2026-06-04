using System;
using System.Collections.Generic;
using System.IO;

namespace Garupan.Content;

/// <summary>
/// Per-chassis drivetrain and tracked-contact calibration. These values belong to game
/// content, not the Opus solver: two vehicles with the same mass can still have different
/// gearboxes, engine braking, traverse response, and skid-steer scrub.
/// </summary>
public sealed record GroundDriveSpec(
    string Id,
    IReadOnlyList<double> ForwardGearRatios,
    double ReverseGearRatio,
    double FinalDriveRatio,
    double TorqueIdleRpm,
    double TorquePeakRpm,
    double TorqueRedlineRpm,
    double IdleRpm,
    double UpshiftRpm,
    double DownshiftRpm,
    double EngineBrakingRatePerSecond,
    double MaximumHullTraverseRadiansPerSecond,
    double TurningResistanceCoefficientSeconds);

/// <summary>Reusable historical drivetrain families loaded from
/// <c>data/ground-drives.csv</c>.</summary>
public static class GroundDriveCatalog
{
    private const string EmbeddedResourceName = "Garupan.Content.ground-drives.csv";

    private static readonly Lazy<IReadOnlyList<GroundDriveSpec>> Drives = new(LoadEmbedded);
    private static readonly Lazy<IReadOnlyDictionary<string, GroundDriveSpec>> ById =
        new(() => BuildIndex(Drives.Value));

    public static IReadOnlyList<GroundDriveSpec> All => Drives.Value;

    public static GroundDriveSpec GermanMedium => RequireById("german_medium");
    public static GroundDriveSpec GermanHeavy => RequireById("german_heavy");
    public static GroundDriveSpec AmericanMedium => RequireById("american_medium");
    public static GroundDriveSpec Interwar => RequireById("interwar");
    public static GroundDriveSpec BritishInfantry => RequireById("british_infantry");
    public static GroundDriveSpec SovietMedium => RequireById("soviet_medium");
    public static GroundDriveSpec HeavyHowitzer => RequireById("heavy_howitzer");
    public static GroundDriveSpec FrenchHeavy => RequireById("french_heavy");

    /// <summary>Resolves a drivetrain family by its stable id; null when the id is unknown.</summary>
    public static GroundDriveSpec? FindById(string id) => ById.Value.TryGetValue(id, out var drive) ? drive : null;

    public static GroundDriveSpec RequireById(string id) =>
        FindById(id) ?? throw new KeyNotFoundException($"Ground-drive catalogue has no profile with id \"{id}\".");

    private static IReadOnlyList<GroundDriveSpec> LoadEmbedded()
    {
        var assembly = typeof(GroundDriveCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded ground-drive catalogue \"{EmbeddedResourceName}\" is missing from {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return GroundDriveCsv.Parse(reader.ReadToEnd());
    }

    private static IReadOnlyDictionary<string, GroundDriveSpec> BuildIndex(IReadOnlyList<GroundDriveSpec> drives)
    {
        var byId = new Dictionary<string, GroundDriveSpec>(StringComparer.Ordinal);
        foreach (var drive in drives)
        {
            byId.Add(drive.Id, drive);
        }

        return byId;
    }
}
