using System.IO;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

/// <summary>Parser coverage for <see cref="MatchModeCsv"/>. Pins the schema +
/// validation rules so a malformed match-mode CSV surfaces at boot, never on the first
/// PLAY click.</summary>
public sealed class MatchModeCsvTests
{
    private const string Header = "id,kind,name_key,summary_key,lobby_capacity,respawn_limit,commander_led";

    [Fact]
    public void Parse_loads_the_closed_alpha_lineup()
    {
        var csv = $"""
            {Header}
            hungry_battles,FreeForAll,lobby.mode.hungry.name,lobby.mode.hungry.summary,20,3,false
            tactical_5v5,TeamTactical,lobby.mode.tactical.name,lobby.mode.tactical.summary,10,1,true
            """;
        var catalog = MatchModeCsv.Parse(csv);

        catalog.Count.Should().Be(2);
        catalog.Modes[0].Id.Should().Be("hungry_battles");
        catalog.Modes[0].Kind.Should().Be(MatchModeKind.FreeForAll);
        catalog.Modes[0].LobbyCapacity.Should().Be(20);
        catalog.Modes[0].RespawnLimit.Should().Be(3);
        catalog.Modes[0].IsCommanderLed.Should().BeFalse();

        catalog.Modes[1].Id.Should().Be("tactical_5v5");
        catalog.Modes[1].Kind.Should().Be(MatchModeKind.TeamTactical);
        catalog.Modes[1].IsCommanderLed.Should().BeTrue();
    }

    [Fact]
    public void Parse_preserves_csv_declaration_order()
    {
        var csv = $"""
            {Header}
            tactical_5v5,TeamTactical,a,b,10,1,true
            hungry_battles,FreeForAll,c,d,20,3,false
            """;
        var catalog = MatchModeCsv.Parse(csv);

        catalog.Modes[0].Id.Should().Be("tactical_5v5");
        catalog.Modes[1].Id.Should().Be("hungry_battles");
    }

    [Fact]
    public void Parse_throws_on_header_mismatch()
    {
        var csv = """
            id,name,kind
            x,a,FreeForAll
            """;
        var act = () => MatchModeCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*header mismatch*");
    }

    [Fact]
    public void Parse_throws_on_unknown_kind()
    {
        var csv = $"""
            {Header}
            mystery,Brawl,a,b,5,0,false
            """;
        var act = () => MatchModeCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*unknown kind*Brawl*");
    }

    [Fact]
    public void Parse_throws_on_zero_lobby_capacity()
    {
        var csv = $"""
            {Header}
            empty,FreeForAll,a,b,0,0,false
            """;
        var act = () => MatchModeCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*lobbyCapacity*positive*");
    }

    [Fact]
    public void Parse_throws_on_negative_respawn_limit()
    {
        var csv = $"""
            {Header}
            bad,FreeForAll,a,b,10,-1,false
            """;
        var act = () => MatchModeCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*respawnLimit*non-negative*");
    }

    [Fact]
    public void Parse_throws_when_free_for_all_claims_commander()
    {
        var csv = $"""
            {Header}
            confused,FreeForAll,a,b,20,3,true
            """;
        var act = () => MatchModeCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*Free-for-all*");
    }

    [Fact]
    public void Parse_throws_on_duplicate_id()
    {
        var csv = $"""
            {Header}
            hungry_battles,FreeForAll,a,b,20,3,false
            hungry_battles,TeamTactical,c,d,10,1,true
            """;
        var act = () => MatchModeCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*duplicate id*hungry_battles*");
    }

    [Fact]
    public void Parse_throws_on_missing_columns()
    {
        var csv = $"""
            {Header}
            short,FreeForAll,a,b,20
            """;
        var act = () => MatchModeCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*7 columns*");
    }

    [Fact]
    public void Parse_throws_when_only_header_present()
    {
        var act = () => MatchModeCsv.Parse(Header);
        act.Should().Throw<InvalidDataException>().WithMessage("*at least a header*");
    }

    [Fact]
    public void LoadFile_throws_FileNotFoundException_for_missing_path()
    {
        var act = () => MatchModeCsv.LoadFile("nonexistent-match-modes.csv");
        act.Should().Throw<FileNotFoundException>();
    }
}
