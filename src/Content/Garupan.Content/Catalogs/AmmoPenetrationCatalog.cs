using System;
using System.Collections.Generic;
using System.IO;

namespace Garupan.Content;

/// <summary>
/// Per-round penetration tables loaded from <c>data/ammo-penetration.csv</c> (embedded as
/// <c>ammo-penetration.csv</c>). Resolved by ammo id, so the spawn pipeline bakes the matching
/// <see cref="PenetrationCurve"/> onto the chambered round. Loading is lazy + cached; a malformed
/// catalogue surfaces on first access (see <see cref="AmmoPenetrationCsv"/>).
/// </summary>
public static class AmmoPenetrationCatalog
{
    private const string EmbeddedResourceName = "Garupan.Content.ammo-penetration.csv";

    private static readonly Lazy<IReadOnlyDictionary<string, PenetrationCurve>> Curves = new(LoadEmbedded);

    /// <summary>Resolves a round's penetration table by ammo id; null when the id has no row.</summary>
    public static PenetrationCurve? FindById(string id) =>
        Curves.Value.TryGetValue(id, out var curve) ? curve : null;

    public static PenetrationCurve RequireById(string id) =>
        FindById(id) ?? throw new KeyNotFoundException($"Ammo-penetration catalogue has no table for ammo id \"{id}\".");

    private static IReadOnlyDictionary<string, PenetrationCurve> LoadEmbedded()
    {
        var assembly = typeof(AmmoPenetrationCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded ammo-penetration catalogue \"{EmbeddedResourceName}\" is missing from {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        var byId = new Dictionary<string, PenetrationCurve>(StringComparer.Ordinal);
        foreach (var curve in AmmoPenetrationCsv.Parse(reader.ReadToEnd()))
        {
            byId.Add(curve.AmmoId, curve);
        }

        return byId;
    }
}
