using System.IO;
using System.Numerics;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

/// <summary>Parser-level coverage for <see cref="SchoolPaletteCsv"/>. The pair-level
/// tests in <c>SchoolPaletteTests</c> verify the catalog API; these tests pin the
/// parsing rules: header validation, malformed rows, lower-case + mixed-case school
/// names, numeric parsing errors, blank-line tolerance.</summary>
public sealed class SchoolPaletteCsvTests
{
    [Fact]
    public void Parse_lowercase_school_name_resolves_to_canonical_enum_value()
    {
        const string csv = """
            school,r,g,b,a,canon_source
            rival_echo,0.5,0.5,0.6,1.0,test
            """;
        var palette = SchoolPaletteCsv.Parse(csv);
        palette.PaintFactor(OpponentSchool.RivalEcho).Should().Be(new Vector4(0.5f, 0.5f, 0.6f, 1.0f));
    }

    [Fact]
    public void Parse_mixed_case_school_name_also_resolves_via_case_insensitive_compare()
    {
        const string csv = """
            school,r,g,b,a,canon_source
            RivalFoxtrot,0.4,0.5,0.3,1.0,test
            """;
        var palette = SchoolPaletteCsv.Parse(csv);
        palette.Contains(OpponentSchool.RivalFoxtrot).Should().BeTrue();
    }

    [Fact]
    public void Parse_throws_on_unknown_school_name()
    {
        const string csv = """
            school,r,g,b,a,canon_source
            atlantis,0.5,0.5,0.5,1.0,not a canon school
            """;
        var act = () => SchoolPaletteCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*unknown school*atlantis*");
    }

    [Fact]
    public void Parse_throws_on_header_mismatch()
    {
        const string csv = """
            school,red,green,blue,alpha,canon_source
            player_school,1.0,1.0,1.0,1.0,test
            """;
        var act = () => SchoolPaletteCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*header mismatch*");
    }

    [Fact]
    public void Parse_throws_when_csv_has_only_a_header_and_no_data_rows()
    {
        const string csv = "school,r,g,b,a,canon_source";
        var act = () => SchoolPaletteCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*at least a header*");
    }

    [Fact]
    public void Parse_throws_on_non_numeric_channel_value()
    {
        const string csv = """
            school,r,g,b,a,canon_source
            player_school,one,1.0,1.0,1.0,test
            """;
        var act = () => SchoolPaletteCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*column \"r\"*one*");
    }

    [Fact]
    public void Parse_throws_on_NaN_channel_value()
    {
        const string csv = """
            school,r,g,b,a,canon_source
            player_school,NaN,1.0,1.0,1.0,test
            """;
        var act = () => SchoolPaletteCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*must be finite*");
    }

    [Fact]
    public void Parse_throws_on_duplicate_school_rows()
    {
        const string csv = """
            school,r,g,b,a,canon_source
            player_school,1.0,1.0,1.0,1.0,first
            player_school,0.5,0.5,0.5,1.0,duplicate
            """;
        var act = () => SchoolPaletteCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*more than once*");
    }

    [Fact]
    public void Parse_tolerates_blank_lines_in_the_middle_of_the_csv()
    {
        const string csv = "school,r,g,b,a,canon_source\nplayer_school,1.0,1.0,1.0,1.0,first\n\nrival_echo,0.5,0.5,0.6,1.0,second\n";
        var palette = SchoolPaletteCsv.Parse(csv);
        palette.Count.Should().Be(2);
    }

    [Fact]
    public void Parse_tolerates_whitespace_around_numeric_values()
    {
        const string csv = """
            school,r,g,b,a,canon_source
            player_school, 1.0 , 1.0 , 1.0 , 1.0 ,trim test
            """;
        var palette = SchoolPaletteCsv.Parse(csv);
        palette.PaintFactor(OpponentSchool.PlayerSchool).Should().Be(Vector4.One);
    }

    [Fact]
    public void LoadFile_throws_FileNotFoundException_for_missing_path()
    {
        var act = () => SchoolPaletteCsv.LoadFile("nonexistent-school-palette.csv");
        act.Should().Throw<FileNotFoundException>();
    }
}
