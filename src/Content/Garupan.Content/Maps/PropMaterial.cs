using System;
using System.Collections.Generic;
using System.IO;

namespace Garupan.Content;

/// <summary>
/// Mechanical properties of the material a prop's standing member is made of — everything the
/// destruction physics needs to turn a size into a resistance. These are real material
/// constants (modulus of rupture, the deflection a member sustains before its base fails, bulk
/// density), not gameplay tuning knobs, so the same numbers would describe the material in any
/// context.
/// </summary>
/// <param name="Name">Stable id referenced by <see cref="PropKindCatalog"/>.</param>
/// <param name="ModulusOfRupturePa">Bending stress at which the base section fails (Pa).</param>
/// <param name="FailureDeflectionRadians">Angular deflection the member sustains before the
/// base lets go — the lever through which the rupture moment does its breaking work.</param>
/// <param name="DensityKgPerCubicMeter">Bulk density, used to weigh the member from its size.</param>
public sealed record PropMaterial(
    string Name,
    float ModulusOfRupturePa,
    float FailureDeflectionRadians,
    float DensityKgPerCubicMeter);

/// <summary>The named materials a city's destructibles are built from. The physical constants
/// live in <c>data/prop-materials.csv</c>, embedded as the baseline authoring catalogue so values
/// stay reviewable data rather than C# tuning literals.</summary>
public static class PropMaterialCatalog
{
    private const string EmbeddedResourceName = "Garupan.Content.prop-materials.csv";

    private static readonly Lazy<IReadOnlyDictionary<string, PropMaterial>> Materials = new(LoadEmbedded);

    public static PropMaterial RequireById(string id) =>
        Materials.Value.TryGetValue(id, out var material)
            ? material
            : throw new KeyNotFoundException($"Prop-material catalogue has no material with id \"{id}\".");

    private static IReadOnlyDictionary<string, PropMaterial> LoadEmbedded()
    {
        var assembly = typeof(PropMaterialCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded prop-material catalogue \"{EmbeddedResourceName}\" is missing from {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return PropMaterialCsv.Parse(reader.ReadToEnd());
    }
}
