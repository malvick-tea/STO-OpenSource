using System;
using System.Collections.Generic;
using System.IO;

namespace Garupan.Content;

/// <summary>Per-chassis gun-installation geometry in metres and degrees. The simulation
/// uses it to place a round at the actual muzzle instead of assuming one model's barrel.</summary>
public sealed record GunMountSpec(
    string Id,
    double MinPitchDegrees,
    double MaxPitchDegrees,
    double TrunnionForwardMeters,
    double TrunnionHeightMeters,
    double BarrelLengthMeters);

/// <summary>Data-authored gun-installation profiles loaded from
/// <c>data/gun-mounts.csv</c>.</summary>
public static class GunMountCatalog
{
    private const string EmbeddedResourceName = "Garupan.Content.gun-mounts.csv";

    private static readonly Lazy<IReadOnlyList<GunMountSpec>> Mounts = new(LoadEmbedded);
    private static readonly Lazy<IReadOnlyDictionary<string, GunMountSpec>> ById =
        new(() => BuildIndex(Mounts.Value));

    public static IReadOnlyList<GunMountSpec> All => Mounts.Value;

    public static GunMountSpec MountMediumA => RequireById("mount_medium_a");
    public static GunMountSpec VehicleHeavyA => RequireById("vehicle_heavy_a");
    public static GunMountSpec MediumC76 => RequireById("medium_c_76");
    public static GunMountSpec MountLightA => RequireById("mount_light_a");
    public static GunMountSpec MountMediumB => RequireById("mount_medium_b");
    public static GunMountSpec VehicleMediumD => RequireById("vehicle_medium_d");
    public static GunMountSpec VehicleHeavyB => RequireById("vehicle_heavy_b");
    public static GunMountSpec HeavyC => RequireById("heavy_c");
    public static GunMountSpec VehicleMediumE => RequireById("vehicle_medium_e");
    public static GunMountSpec VehicleHeavyD => RequireById("vehicle_heavy_d");
    public static GunMountSpec MediumF => RequireById("medium_f");

    /// <summary>Resolves a mount geometry by its stable id; null when the id is unknown.</summary>
    public static GunMountSpec? FindById(string id) => ById.Value.TryGetValue(id, out var mount) ? mount : null;

    public static GunMountSpec RequireById(string id) =>
        FindById(id) ?? throw new KeyNotFoundException($"Gun-mount catalogue has no profile with id \"{id}\".");

    private static IReadOnlyList<GunMountSpec> LoadEmbedded()
    {
        var assembly = typeof(GunMountCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded gun-mount catalogue \"{EmbeddedResourceName}\" is missing from {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return GunMountCsv.Parse(reader.ReadToEnd());
    }

    private static IReadOnlyDictionary<string, GunMountSpec> BuildIndex(IReadOnlyList<GunMountSpec> mounts)
    {
        var byId = new Dictionary<string, GunMountSpec>(StringComparer.Ordinal);
        foreach (var mount in mounts)
        {
            byId.Add(mount.Id, mount);
        }

        return byId;
    }
}
