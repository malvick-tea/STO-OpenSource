using System;
using System.Collections.Generic;
using System.IO;

namespace Garupan.Content;

/// <summary>Data-authored gun catalogue loaded from <c>data/guns.csv</c>.</summary>
public static class GunCatalog
{
    private const string EmbeddedResourceName = "Garupan.Content.guns.csv";

    private static readonly Lazy<IReadOnlyList<GunSpec>> Guns = new(LoadEmbedded);
    private static readonly Lazy<IReadOnlyDictionary<string, GunSpec>> Index =
        new(() => BuildIndex(Guns.Value));

    public static IReadOnlyList<GunSpec> All => Guns.Value;

    public static GunSpec GunMediumA => RequireById("gun_medium_a");
    public static GunSpec GunHeavyA => RequireById("gun_heavy_a");
    public static GunSpec GunMediumC => RequireById("gun_medium_c");
    public static GunSpec GunLightA => RequireById("gun_light_a");
    public static GunSpec GunAssaultA => RequireById("gun_assault_a");
    public static GunSpec GunMediumD => RequireById("gun_medium_d");
    public static GunSpec GunHeavyB => RequireById("gun_heavy_b");
    public static GunSpec GunHeavyC => RequireById("gun_heavy_c");
    public static GunSpec GunMediumE => RequireById("gun_medium_e");
    public static GunSpec GunHeavyD => RequireById("gun_heavy_d");
    public static GunSpec GunMediumF => RequireById("gun_medium_f");

    public static GunSpec? FindById(string id) =>
        Index.Value.TryGetValue(id, out var gun) ? gun : null;

    public static GunSpec RequireById(string id) =>
        FindById(id) ?? throw new KeyNotFoundException($"Gun catalogue has no gun with id \"{id}\".");

    public static GunSpec? FindByCaliber(string caliber)
    {
        foreach (var gun in All)
        {
            if (string.Equals(gun.Caliber, caliber, StringComparison.Ordinal))
            {
                return gun;
            }
        }

        return null;
    }

    private static IReadOnlyList<GunSpec> LoadEmbedded()
    {
        var assembly = typeof(GunCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded gun catalogue \"{EmbeddedResourceName}\" is missing from {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return GunCsv.Parse(reader.ReadToEnd());
    }

    private static IReadOnlyDictionary<string, GunSpec> BuildIndex(IReadOnlyList<GunSpec> all)
    {
        var byId = new Dictionary<string, GunSpec>(StringComparer.Ordinal);
        foreach (var gun in all)
        {
            byId.Add(gun.Id, gun);
        }

        return byId;
    }
}
