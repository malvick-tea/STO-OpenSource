using System;
using System.Collections.Generic;
using System.IO;

namespace Garupan.Content;

/// <summary>How a prop kind behaves and what it is made of — the bridge from the separator's
/// coarse classification to the physics. Trees and poles are rooted members that hinge over;
/// signs, bins, and other clutter come apart. Bundled so a new kind is one row, never a code
/// path: the simulation reads behaviour + material here and derives every threshold.</summary>
public sealed record PropArchetype(PropBehavior Behavior, PropMaterial Material);

/// <summary>Maps each <see cref="PropKind"/> to its failure behaviour and material. The mapping
/// lives in <c>data/prop-kinds.csv</c>; code only owns strict loading and lookup.</summary>
public static class PropKindCatalog
{
    private const string EmbeddedResourceName = "Garupan.Content.prop-kinds.csv";

    private static readonly Lazy<IReadOnlyDictionary<PropKind, PropArchetype>> Archetypes = new(LoadEmbedded);

    /// <summary>The behaviour + material for a kind. A missing mapping is a broken content
    /// catalogue and fails loudly instead of silently inventing physics.</summary>
    public static PropArchetype For(PropKind kind) =>
        Archetypes.Value.TryGetValue(kind, out var archetype)
            ? archetype
            : throw new KeyNotFoundException($"Prop-kind catalogue has no mapping for \"{kind}\".");

    private static IReadOnlyDictionary<PropKind, PropArchetype> LoadEmbedded()
    {
        var assembly = typeof(PropKindCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded prop-kind catalogue \"{EmbeddedResourceName}\" is missing from {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return PropKindCsv.Parse(reader.ReadToEnd(), PropMaterialCatalog.RequireById);
    }
}
