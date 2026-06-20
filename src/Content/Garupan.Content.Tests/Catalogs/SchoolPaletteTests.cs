using System;
using System.IO;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

/// <summary>Verifies <see cref="SchoolPalette"/> + <see cref="SchoolPaletteCsv"/> as a
/// pair. Tests run against an in-memory CSV that mirrors the canonical
/// <c>data/school-palette.csv</c> shape — no filesystem dependency in the unit suite.
/// Specific colour values are tuneable in the CSV; these tests pin the invariants (every
/// enum value covered; alpha = 1; no all-zero tint; tints are pairwise distinct) plus a
/// handful of canon-direction sanity checks (RivalEcho desaturated, RivalDelta blue-shifted).</summary>
public sealed class SchoolPaletteTests
{
    private const string CanonicalCsv = """
        school,r,g,b,a,canon_source
        player_school,1.0,1.0,1.0,1.0,authored baseline
        rival_alpha,0.95,0.82,0.65,1.0,khaki-brown
        rival_bravo,1.0,0.96,0.85,1.0,warm olive
        rival_charlie,1.0,0.92,0.62,1.0,desert tan
        rival_delta,0.85,0.88,0.95,1.0,winter camo
        rival_echo,0.55,0.55,0.62,1.0,DarkGrey
        rival_foxtrot,0.85,0.92,0.58,1.0,olive-green
        rival_golf,1.0,0.9,0.58,1.0,yellow-khaki
        """;

    [Fact]
    public void Loads_every_OpponentSchool_value_from_the_canonical_csv()
    {
        var palette = SchoolPaletteCsv.Parse(CanonicalCsv);
        foreach (var school in Enum.GetValues<OpponentSchool>())
        {
            palette.Contains(school).Should().BeTrue($"school {school} must be present");
        }
    }

    [Fact]
    public void PlayerSchool_is_identity_tint_so_authored_albedo_renders_unchanged()
    {
        var palette = SchoolPaletteCsv.Parse(CanonicalCsv);
        palette.PaintFactor(OpponentSchool.PlayerSchool).Should().Be(Vector4.One);
    }

    [Theory]
    [InlineData(OpponentSchool.PlayerSchool)]
    [InlineData(OpponentSchool.RivalAlpha)]
    [InlineData(OpponentSchool.RivalBravo)]
    [InlineData(OpponentSchool.RivalCharlie)]
    [InlineData(OpponentSchool.RivalDelta)]
    [InlineData(OpponentSchool.RivalEcho)]
    [InlineData(OpponentSchool.RivalFoxtrot)]
    [InlineData(OpponentSchool.RivalGolf)]
    public void Every_school_has_full_alpha(OpponentSchool school)
    {
        var palette = SchoolPaletteCsv.Parse(CanonicalCsv);
        palette.PaintFactor(school).W.Should().Be(1f);
    }

    [Theory]
    [InlineData(OpponentSchool.PlayerSchool)]
    [InlineData(OpponentSchool.RivalAlpha)]
    [InlineData(OpponentSchool.RivalBravo)]
    [InlineData(OpponentSchool.RivalCharlie)]
    [InlineData(OpponentSchool.RivalDelta)]
    [InlineData(OpponentSchool.RivalEcho)]
    [InlineData(OpponentSchool.RivalFoxtrot)]
    [InlineData(OpponentSchool.RivalGolf)]
    public void Every_school_has_positive_rgb(OpponentSchool school)
    {
        var palette = SchoolPaletteCsv.Parse(CanonicalCsv);
        var t = palette.PaintFactor(school);
        t.X.Should().BeGreaterThan(0f);
        t.Y.Should().BeGreaterThan(0f);
        t.Z.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void All_eight_school_tints_are_pairwise_unique()
    {
        var palette = SchoolPaletteCsv.Parse(CanonicalCsv);
        var schools = Enum.GetValues<OpponentSchool>();
        var tints = schools.Select(palette.PaintFactor).ToArray();
        tints.Distinct().Should().HaveSameCount(schools);
    }

    [Fact]
    public void RivalEcho_is_a_cool_grey_shift_per_canon_DarkGrey()
    {
        var palette = SchoolPaletteCsv.Parse(CanonicalCsv);
        var t = palette.PaintFactor(OpponentSchool.RivalEcho);
        t.X.Should().BeLessThan(1f);
        t.Y.Should().BeLessThan(1f);
        t.Z.Should().BeLessThan(1f);
    }

    [Fact]
    public void RivalDelta_skews_blue_dominant_per_canon_winter_camo()
    {
        var palette = SchoolPaletteCsv.Parse(CanonicalCsv);
        var t = palette.PaintFactor(OpponentSchool.RivalDelta);
        t.Z.Should().BeGreaterThan(t.X);
    }

    [Fact]
    public void PaintFactor_throws_when_school_is_missing_from_loaded_palette()
    {
        const string partial = """
            school,r,g,b,a,canon_source
            player_school,1.0,1.0,1.0,1.0,only school in partial palette
            """;
        var palette = SchoolPaletteCsv.Parse(partial);

        var act = () => palette.PaintFactor(OpponentSchool.RivalEcho);
        act.Should().Throw<System.Collections.Generic.KeyNotFoundException>();
    }
}
