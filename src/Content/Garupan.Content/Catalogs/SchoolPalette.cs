using System;
using System.Collections.Generic;
using System.Numerics;

namespace Garupan.Content;

/// <summary>
/// Loaded canon paint-tint catalogue. For each <see cref="OpponentSchool"/>, returns a
/// <see cref="Vector4"/> albedo factor that the forward scene renderer multiplies onto
/// the authored tank texture. The result is a same-asset-different-camo render — same
/// glTF model, different paint per school — without uploading separate albedo textures.
/// </summary>
/// <remarks>
/// <para>
/// Instances are produced by <see cref="SchoolPaletteCsv.LoadFile"/> or
/// <see cref="SchoolPaletteCsv.Parse"/>. The canonical CSV ships at
/// <c>data/school-palette.csv</c> with one row per school plus a <c>canon_source</c>
/// column attributing the colour to its anime / a later season reference. The catalog is
/// data-driven so artists / loremasters can tune values without touching C#.
/// </para>
/// <para>
/// Why this lives in <c>Content</c> rather than the renderer: the canon paint scheme is
/// authoring-side data, not GPU plumbing. Localisation, narrative, and gameplay logic
/// all need to refer to schools and their visual identity (UI school crest tint,
/// briefing portrait background, replay scoreboard accent, etc.), and the renderer is
/// only one consumer. Per ADR-0014 the Content layer owns canonical game data;
/// Engine.Renderer consumes it.
/// </para>
/// </remarks>
public sealed class SchoolPalette
{
    private readonly Dictionary<OpponentSchool, Vector4> _factors;

    internal SchoolPalette(Dictionary<OpponentSchool, Vector4> factors)
    {
        ArgumentNullException.ThrowIfNull(factors);
        _factors = factors;
    }

    /// <summary>Number of schools defined in this palette. Equal to
    /// <see cref="Enum.GetValues"/>'s length when the canonical CSV is loaded.</summary>
    public int Count => _factors.Count;

    /// <summary>Returns the canon paint tint for <paramref name="school"/>. Throws
    /// <see cref="KeyNotFoundException"/> if the palette wasn't loaded with that school
    /// — the canonical <c>data/school-palette.csv</c> covers every enum value, so this
    /// surfaces a real data-side bug rather than silently rendering at identity tint.</summary>
    public Vector4 PaintFactor(OpponentSchool school)
    {
        if (!_factors.TryGetValue(school, out var tint))
        {
            throw new KeyNotFoundException(
                $"School palette has no entry for \"{school}\". Check data/school-palette.csv coverage.");
        }

        return tint;
    }

    /// <summary>Whether this palette has an entry for <paramref name="school"/>.</summary>
    public bool Contains(OpponentSchool school) => _factors.ContainsKey(school);
}
