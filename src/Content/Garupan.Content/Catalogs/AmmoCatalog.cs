using System;
using System.Collections.Generic;
using System.IO;

namespace Garupan.Content;

/// <summary>Data-authored ammunition catalogue loaded from <c>data/ammo.csv</c>.</summary>
public static class AmmoCatalog
{
    private const string EmbeddedResourceName = "Garupan.Content.ammo.csv";

    private static readonly Lazy<IReadOnlyList<AmmoSpec>> Rounds = new(LoadEmbedded);
    private static readonly Lazy<IReadOnlyDictionary<string, AmmoSpec>> Index =
        new(() => BuildIndex(Rounds.Value));

    public static IReadOnlyList<AmmoSpec> All => Rounds.Value;

    public static AmmoSpec AmmoMediumAAp => RequireById("ammo_medium_a_ap");
    public static AmmoSpec AmmoHeavyAAp => RequireById("ammo_heavy_a_ap");
    public static AmmoSpec AmmoMediumCAp => RequireById("ammo_medium_c_ap");
    public static AmmoSpec AmmoMediumAApcr => RequireById("ammo_medium_a_apcr");
    public static AmmoSpec AmmoHeavyAApcr => RequireById("ammo_heavy_a_apcr");
    public static AmmoSpec AmmoMediumCHvap => RequireById("ammo_medium_c_hvap");
    public static AmmoSpec AmmoMediumAHeat => RequireById("ammo_medium_a_heat");
    public static AmmoSpec AmmoMediumAHe => RequireById("ammo_medium_a_he");
    public static AmmoSpec AmmoLightAAp => RequireById("ammo_light_a_ap");
    public static AmmoSpec AmmoAssaultAAp => RequireById("ammo_assault_a_ap");
    public static AmmoSpec AmmoMediumDAp => RequireById("ammo_medium_d_ap");
    public static AmmoSpec AmmoHeavyBAp => RequireById("ammo_heavy_b_ap");
    public static AmmoSpec AmmoHeavyCAp => RequireById("ammo_heavy_c_ap");
    public static AmmoSpec AmmoMediumEAp => RequireById("ammo_medium_e_ap");
    public static AmmoSpec AmmoHeavyDHe => RequireById("ammo_heavy_d_he");
    public static AmmoSpec AmmoMediumFAp => RequireById("ammo_medium_f_ap");

    public static AmmoSpec? FindById(string id) =>
        Index.Value.TryGetValue(id, out var round) ? round : null;

    public static AmmoSpec RequireById(string id) =>
        FindById(id) ?? throw new KeyNotFoundException($"Ammo catalogue has no round with id \"{id}\".");

    private static IReadOnlyList<AmmoSpec> LoadEmbedded()
    {
        var assembly = typeof(AmmoCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded ammo catalogue \"{EmbeddedResourceName}\" is missing from {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return AmmoCsv.Parse(reader.ReadToEnd());
    }

    private static IReadOnlyDictionary<string, AmmoSpec> BuildIndex(IReadOnlyList<AmmoSpec> all)
    {
        var byId = new Dictionary<string, AmmoSpec>(StringComparer.Ordinal);
        foreach (var round in all)
        {
            byId.Add(round.Id, round);
        }

        return byId;
    }
}
