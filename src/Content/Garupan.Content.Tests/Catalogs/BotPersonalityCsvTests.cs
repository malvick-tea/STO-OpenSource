using System.IO;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

/// <summary>Parser-level coverage for <see cref="BotPersonalityCsv"/>. The catalog-API
/// tests in <c>BotPersonalityCatalogTests</c> verify the lookup semantics; these tests
/// pin the parsing rules: header validation, malformed rows, range checks, blank-line
/// tolerance, etc.</summary>
public sealed class BotPersonalityCsvTests
{
    private const string Header = "school,engage_range_m,throttle_scale,alignment_tolerance_radians,canon_source";

    [Fact]
    public void Parse_loads_a_well_formed_row()
    {
        var csv = $"""
            {Header}
            rival_echo,90,0.7,0.03,test
            """;
        var catalog = BotPersonalityCsv.Parse(csv);
        var p = catalog.Resolve(OpponentSchool.RivalEcho);
        p.EngageRangeMeters.Should().Be(90f);
        p.ThrottleScale.Should().Be(0.7f);
        p.AlignmentToleranceRadians.Should().Be(0.03f);
    }

    [Fact]
    public void Parse_is_case_insensitive_on_school_names()
    {
        var csv = $"""
            {Header}
            RivalFoxtrot,40,0.9,0.1,test
            """;
        var catalog = BotPersonalityCsv.Parse(csv);
        catalog.Contains(OpponentSchool.RivalFoxtrot).Should().BeTrue();
    }

    [Fact]
    public void Parse_throws_on_unknown_school_name()
    {
        var csv = $"""
            {Header}
            atlantis,50,0.5,0.05,not canon
            """;
        var act = () => BotPersonalityCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*unknown school*atlantis*");
    }

    [Fact]
    public void Parse_throws_on_header_mismatch()
    {
        var csv = """
            school,range,throttle,alignment,canon_source
            player_school,60,0.5,0.05,test
            """;
        var act = () => BotPersonalityCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*header mismatch*");
    }

    [Fact]
    public void Parse_throws_when_only_header_is_present()
    {
        var act = () => BotPersonalityCsv.Parse(Header);
        act.Should().Throw<InvalidDataException>().WithMessage("*at least a header*");
    }

    [Fact]
    public void Parse_throws_on_non_finite_engage_range()
    {
        var csv = $"""
            {Header}
            player_school,NaN,0.5,0.05,test
            """;
        var act = () => BotPersonalityCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*must be finite*");
    }

    [Fact]
    public void Parse_throws_on_throttle_above_one()
    {
        var csv = $"""
            {Header}
            player_school,60,1.5,0.05,test
            """;
        var act = () => BotPersonalityCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*throttleScale*[0, 1]*");
    }

    [Fact]
    public void Parse_throws_on_negative_throttle()
    {
        var csv = $"""
            {Header}
            player_school,60,-0.1,0.05,test
            """;
        var act = () => BotPersonalityCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*throttleScale*[0, 1]*");
    }

    [Fact]
    public void Parse_throws_on_alignment_above_pi()
    {
        var csv = $"""
            {Header}
            player_school,60,0.5,3.5,test
            """;
        var act = () => BotPersonalityCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*alignmentToleranceRadians*");
    }

    [Fact]
    public void Parse_throws_on_zero_engage_range()
    {
        var csv = $"""
            {Header}
            player_school,0,0.5,0.05,test
            """;
        var act = () => BotPersonalityCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*engageRangeMeters*positive*");
    }

    [Fact]
    public void Parse_throws_on_duplicate_school_rows()
    {
        var csv = $"""
            {Header}
            player_school,60,0.5,0.05,first
            player_school,55,0.45,0.04,duplicate
            """;
        var act = () => BotPersonalityCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*more than once*");
    }

    [Fact]
    public void Parse_tolerates_blank_lines()
    {
        var csv = $"{Header}\nplayer_school,60,0.5,0.05,test\n\nrival_echo,90,0.7,0.03,test\n";
        var catalog = BotPersonalityCsv.Parse(csv);
        catalog.Count.Should().Be(2);
    }

    [Fact]
    public void Parse_tolerates_whitespace_inside_numeric_cells()
    {
        var csv = $"""
            {Header}
            player_school, 60 , 0.5 , 0.05 ,whitespace test
            """;
        var catalog = BotPersonalityCsv.Parse(csv);
        catalog.Resolve(OpponentSchool.PlayerSchool).EngageRangeMeters.Should().Be(60f);
    }

    [Fact]
    public void LoadFile_throws_FileNotFoundException_for_missing_path()
    {
        var act = () => BotPersonalityCsv.LoadFile("nonexistent-ai-personalities.csv");
        act.Should().Throw<FileNotFoundException>();
    }
}
