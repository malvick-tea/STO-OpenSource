using System.Collections.Generic;
using System.Numerics;
using Garupan.Content;

namespace Garupan.Client.Ui.Match.Network;

/// <summary>One axis-aligned box that makes up a prop's blockout silhouette, expressed relative to
/// the prop's catalogue size so a single profile fits every instance of a kind regardless of its
/// authored diameter / height. Most props are a single box; a tree is a trunk box plus a wider
/// canopy box.</summary>
/// <param name="RadiusMultiplier">Footprint half-width as a multiple of the prop's base radius
/// (<c>BaseDiameterMeters / 2</c>) — 1 draws the structural cross-section, higher spreads a wide
/// seat or canopy.</param>
/// <param name="BottomFraction">Box underside as a fraction of the prop height (0 = ground).</param>
/// <param name="TopFraction">Box top as a fraction of the prop height (1 = full height).</param>
/// <param name="Tint">RGBA multiplier applied to the box's albedo so the blockout reads as
/// street furniture — a dark steel pole, an orange cone, a green canopy — without per-prop art.</param>
public readonly record struct PropBoxLayer(
    float RadiusMultiplier,
    float BottomFraction,
    float TopFraction,
    Vector4 Tint);

/// <summary>The box layers that draw one <see cref="PropKind"/> as a blockout solid.</summary>
public sealed record PropVisualProfile(IReadOnlyList<PropBoxLayer> Layers);

/// <summary>
/// Maps each <see cref="PropKind"/> to the blockout boxes the match renderer draws for it. This is
/// render-only data — the kind's <em>physics</em> (material, failure behaviour) lives server-side in
/// <see cref="PropKindCatalog"/>; here we only decide how the authoritative prop looks as a grey-city
/// solid until per-kind art lands. Tints are albedo multipliers over the white material slot, so they
/// read as the final colour rather than a camo modulation.
/// </summary>
public static class PropVisualCatalog
{
    // Albedo multipliers over the white material slot. Declared before the profiles that reference
    // them: C# runs static field initialisers in textual order, so a colour used by Default / Profiles
    // must already be assigned or it would read as default(Vector4) — transparent black.
    private static readonly Vector4 Concrete = Rgb(0.62f, 0.62f, 0.60f);
    private static readonly Vector4 Steel = Rgb(0.32f, 0.33f, 0.36f);
    private static readonly Vector4 SignGrey = Rgb(0.70f, 0.71f, 0.72f);
    private static readonly Vector4 Cast = Rgb(0.64f, 0.20f, 0.17f);
    private static readonly Vector4 BinGreen = Rgb(0.30f, 0.40f, 0.33f);
    private static readonly Vector4 Wood = Rgb(0.45f, 0.32f, 0.19f);
    private static readonly Vector4 Drum = Rgb(0.26f, 0.40f, 0.52f);
    private static readonly Vector4 SafetyOrange = Rgb(0.88f, 0.42f, 0.12f);
    private static readonly Vector4 Bark = Rgb(0.34f, 0.25f, 0.16f);
    private static readonly Vector4 Foliage = Rgb(0.24f, 0.42f, 0.20f);

    private static readonly PropVisualProfile Default = Single(1f, Concrete);

    private static readonly IReadOnlyDictionary<PropKind, PropVisualProfile> Profiles =
        new Dictionary<PropKind, PropVisualProfile>
        {
            [PropKind.Tree] = new(new[]
            {
                new PropBoxLayer(1.4f, 0f, 0.5f, Bark),
                new PropBoxLayer(6f, 0.45f, 1f, Foliage),
            }),
            [PropKind.Bush] = Single(4f, Foliage, topFraction: 0.7f),
            [PropKind.LampPost] = Single(1f, Steel),
            [PropKind.TrafficLight] = Single(1.2f, Steel),
            [PropKind.TrafficSign] = Single(1f, SignGrey),
            [PropKind.Hydrant] = Single(1.5f, Cast),
            [PropKind.Bin] = Single(1.3f, BinGreen),
            [PropKind.Bench] = Single(6f, Wood, topFraction: 0.6f),
            [PropKind.Crate] = Single(2f, Wood),
            [PropKind.Barrel] = Single(1.4f, Drum),
            [PropKind.Cone] = Single(1f, SafetyOrange),
            [PropKind.Fence] = Single(5f, Steel, topFraction: 0.8f),
        };

    /// <summary>The blockout profile for a kind, falling back to a plain concrete box for any kind a
    /// future content drop adds before this catalogue does — a new prop renders as a neutral solid
    /// rather than crashing the frame.</summary>
    public static PropVisualProfile For(PropKind kind) =>
        Profiles.TryGetValue(kind, out var profile) ? profile : Default;

    private static PropVisualProfile Single(float radiusMultiplier, Vector4 tint, float topFraction = 1f) =>
        new(new[] { new PropBoxLayer(radiusMultiplier, 0f, topFraction, tint) });

    private static Vector4 Rgb(float r, float g, float b) => new(r, g, b, 1f);
}
