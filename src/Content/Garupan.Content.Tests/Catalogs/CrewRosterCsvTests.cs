using System.IO;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

/// <summary>Parser-level coverage for <see cref="CrewRosterCsv"/>. The
/// <c>PlayerTeamTests</c> suite covers the canon-roster invariants; these tests pin
/// the parsing rules.</summary>
public sealed class CrewRosterCsvTests
{
    private const string Header = "id,given_name,family_name,role,role_key,school_key";

    [Fact]
    public void Parse_loads_a_well_formed_row()
    {
        var csv = $"""
            {Header}
            test_id,Test,Person,Commander,crew.role.commander,test_school
            """;
        var roster = CrewRosterCsv.Parse(csv);
        roster.All.Should().HaveCount(1);
        roster.All[0].Id.Should().Be("test_id");
        roster.All[0].GivenName.Should().Be("Test");
        roster.All[0].Role.Should().Be(CrewRole.Commander);
        roster.SchoolKey.Should().Be("test_school");
    }

    [Fact]
    public void FindById_resolves_loaded_member()
    {
        var csv = $"""
            {Header}
            a_id,Alice,One,Gunner,crew.role.gunner,test_school
            b_id,Bob,Two,Loader,crew.role.loader,test_school
            """;
        var roster = CrewRosterCsv.Parse(csv);
        roster.FindById("a_id")!.GivenName.Should().Be("Alice");
        roster.FindById("b_id")!.Role.Should().Be(CrewRole.Loader);
    }

    [Fact]
    public void FindById_returns_null_for_unknown_id()
    {
        var csv = $"""
            {Header}
            a_id,Alice,One,Gunner,crew.role.gunner,test_school
            """;
        var roster = CrewRosterCsv.Parse(csv);
        roster.FindById("missing").Should().BeNull();
        roster.Contains("missing").Should().BeFalse();
    }

    [Fact]
    public void Parse_throws_on_duplicate_member_id()
    {
        var csv = $"""
            {Header}
            same_id,Alice,One,Gunner,crew.role.gunner,test_school
            same_id,Bob,Two,Loader,crew.role.loader,test_school
            """;
        var act = () => CrewRosterCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*appears more than once*");
    }

    [Fact]
    public void Parse_throws_on_mixed_school_keys_within_one_csv()
    {
        var csv = $"""
            {Header}
            a_id,Alice,One,Gunner,crew.role.gunner,school_a
            b_id,Bob,Two,Loader,crew.role.loader,school_b
            """;
        var act = () => CrewRosterCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*One CSV = one school*");
    }

    [Fact]
    public void Parse_throws_on_unknown_role()
    {
        var csv = $"""
            {Header}
            test_id,Test,Person,Mechanic,crew.role.mechanic,test_school
            """;
        var act = () => CrewRosterCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*unknown role*Mechanic*");
    }

    [Fact]
    public void Parse_is_case_insensitive_on_role()
    {
        var csv = $"""
            {Header}
            test_id,Test,Person,commander,crew.role.commander,test_school
            """;
        var roster = CrewRosterCsv.Parse(csv);
        roster.All[0].Role.Should().Be(CrewRole.Commander);
    }

    [Fact]
    public void Parse_throws_on_empty_id_column()
    {
        var csv = $"""
            {Header}
            ,Test,Person,Commander,crew.role.commander,test_school
            """;
        var act = () => CrewRosterCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*id*empty*");
    }

    [Fact]
    public void Parse_throws_on_header_mismatch()
    {
        var csv = """
            id,name,surname,role,role_key,school
            test_id,Test,Person,Commander,crew.role.commander,test_school
            """;
        var act = () => CrewRosterCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*header mismatch*");
    }

    [Fact]
    public void Parse_throws_when_csv_has_only_a_header()
    {
        var act = () => CrewRosterCsv.Parse(Header);
        act.Should().Throw<InvalidDataException>().WithMessage("*at least a header*");
    }

    [Fact]
    public void LoadFile_throws_FileNotFoundException_for_missing_path()
    {
        var act = () => CrewRosterCsv.LoadFile("nonexistent-player_crew.csv");
        act.Should().Throw<FileNotFoundException>();
    }
}
