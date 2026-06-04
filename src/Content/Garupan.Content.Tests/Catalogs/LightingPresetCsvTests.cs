using System.IO;
using System.Numerics;
using FluentAssertions;
using Garupan.Content;
using Opus.Content;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

public sealed class LightingPresetCsvTests
{
    private const string CanonicalCsv = """
        key,r,g,b,canon_source
        sun_direction,0.4,0.85,0.55,test sun
        sun_colour,1.0,0.95,0.85,test warm sun
        ambient_colour,0.22,0.24,0.28,test slate
        horizon_colour,0.78,0.71,0.60,test tan
        """;

    [Fact]
    public void Parse_extracts_all_four_canon_lighting_channels()
    {
        var preset = LightingPresetCsv.Parse(CanonicalCsv);
        preset.SunColour.Should().Be(new Vector3(1.0f, 0.95f, 0.85f));
        preset.AmbientColour.Should().Be(new Vector3(0.22f, 0.24f, 0.28f));
        preset.HorizonColour.Should().Be(new Vector3(0.78f, 0.71f, 0.60f));
    }

    [Fact]
    public void Parse_normalises_the_sun_direction_vector()
    {
        var preset = LightingPresetCsv.Parse(CanonicalCsv);
        preset.SunDirection.Length().Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void Parse_throws_on_zero_length_sun_direction()
    {
        const string csv = """
            key,r,g,b,canon_source
            sun_direction,0,0,0,zero vector
            sun_colour,1,1,1,test
            ambient_colour,0.2,0.2,0.2,test
            horizon_colour,0.8,0.7,0.6,test
            """;
        var act = () => LightingPresetCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*sun_direction*non-zero*");
    }

    [Fact]
    public void Parse_throws_when_a_required_key_is_missing()
    {
        const string csv = """
            key,r,g,b,canon_source
            sun_direction,0.4,0.85,0.55,test
            sun_colour,1.0,0.95,0.85,test
            ambient_colour,0.22,0.24,0.28,test
            """;
        var act = () => LightingPresetCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*horizon_colour*missing*");
    }

    [Fact]
    public void Parse_throws_on_header_mismatch()
    {
        const string csv = """
            key,red,green,blue,note
            sun_direction,0.4,0.85,0.55,test
            """;
        var act = () => LightingPresetCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*header mismatch*");
    }

    [Fact]
    public void Parse_throws_on_non_finite_channel()
    {
        const string csv = """
            key,r,g,b,canon_source
            sun_direction,0.4,0.85,0.55,test
            sun_colour,Infinity,0.95,0.85,test
            ambient_colour,0.22,0.24,0.28,test
            horizon_colour,0.78,0.71,0.60,test
            """;
        var act = () => LightingPresetCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*must be finite*");
    }

    [Fact]
    public void Parse_throws_on_duplicate_key_rows()
    {
        const string csv = """
            key,r,g,b,canon_source
            sun_direction,0.4,0.85,0.55,test
            sun_direction,0.5,0.5,0.5,duplicate
            sun_colour,1,1,1,test
            ambient_colour,0.2,0.2,0.2,test
            horizon_colour,0.7,0.7,0.7,test
            """;
        var act = () => LightingPresetCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*more than once*");
    }

    [Fact]
    public void LoadFile_throws_for_missing_file()
    {
        var act = () => LightingPresetCsv.LoadFile("nope-lighting.csv");
        act.Should().Throw<FileNotFoundException>();
    }
}
