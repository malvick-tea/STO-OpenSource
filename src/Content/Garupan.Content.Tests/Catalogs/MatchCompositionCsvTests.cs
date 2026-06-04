using System.IO;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

/// <summary>Parser-level tests for <see cref="MatchCompositionCsv"/> + invariants for
/// <see cref="MatchComposition"/>. Composition data flows through the same shape as the
/// canonical <c>data/garage-demo-match.csv</c>; tests use inline CSVs so the suite has
/// no filesystem dependency.</summary>
public sealed class MatchCompositionCsvTests
{
    private const string FourSpawnDemo = """
        tank_id,role,pos_x,pos_y,yaw_radians
        vehicle_medium_a,player,0,0,0
        vehicle_heavy_a,opponent,18,18,3.141593
        vehicle_medium_b,opponent,-15,22,-2.356194
        vehicle_medium_b,opponent,20,-8,2.356194
        """;

    [Fact]
    public void Parse_demo_composition_emits_four_spawns_in_author_order()
    {
        var match = MatchCompositionCsv.Parse(FourSpawnDemo);
        match.Spawns.Should().HaveCount(4);
        match.Spawns[0].TankId.Should().Be("vehicle_medium_a");
        match.Spawns[1].TankId.Should().Be("vehicle_heavy_a");
        match.Spawns[2].TankId.Should().Be("vehicle_medium_b");
        match.Spawns[3].TankId.Should().Be("vehicle_medium_b");
    }

    [Fact]
    public void Parse_distinguishes_player_and_opponent_roles()
    {
        var match = MatchCompositionCsv.Parse(FourSpawnDemo);
        match.Player.TankId.Should().Be("vehicle_medium_a");
        match.Opponents.Should().HaveCount(3);
        match.Opponents.Should().AllSatisfy(s => s.Role.Should().Be(MatchRole.Opponent));
    }

    [Fact]
    public void Parse_reads_position_as_Vector2_in_author_units()
    {
        var match = MatchCompositionCsv.Parse(FourSpawnDemo);
        match.Spawns[1].Position.Should().Be(new Vector2(18f, 18f));
        match.Spawns[2].Position.Should().Be(new Vector2(-15f, 22f));
    }

    [Fact]
    public void Parse_reads_yaw_radians_directly()
    {
        var match = MatchCompositionCsv.Parse(FourSpawnDemo);
        match.Spawns[1].YawRadians.Should().BeApproximately(3.141593f, 1e-5f);
    }

    [Fact]
    public void Parse_accepts_uppercase_role_via_case_insensitive_compare()
    {
        const string csv = """
            tank_id,role,pos_x,pos_y,yaw_radians
            vehicle_medium_a,Player,0,0,0
            vehicle_heavy_a,OPPONENT,5,5,0
            """;
        var match = MatchCompositionCsv.Parse(csv);
        match.Player.Role.Should().Be(MatchRole.Player);
        match.Opponents.Single().Role.Should().Be(MatchRole.Opponent);
    }

    [Fact]
    public void Parse_throws_when_tank_id_does_not_resolve_in_TankRoster()
    {
        const string csv = """
            tank_id,role,pos_x,pos_y,yaw_radians
            vehicle_medium_a,player,0,0,0
            ufo_tank,opponent,5,5,0
            """;
        var act = () => MatchCompositionCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*ufo_tank*does not resolve*");
    }

    [Fact]
    public void Parse_throws_on_unknown_role()
    {
        const string csv = """
            tank_id,role,pos_x,pos_y,yaw_radians
            vehicle_medium_a,player,0,0,0
            vehicle_heavy_a,civilian,5,5,0
            """;
        var act = () => MatchCompositionCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*unknown role*civilian*");
    }

    [Fact]
    public void Parse_throws_on_header_mismatch()
    {
        const string csv = """
            id,role,x,y,yaw
            vehicle_medium_a,player,0,0,0
            """;
        var act = () => MatchCompositionCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*header mismatch*");
    }

    [Fact]
    public void Parse_throws_when_position_is_non_finite()
    {
        const string csv = """
            tank_id,role,pos_x,pos_y,yaw_radians
            vehicle_medium_a,player,0,0,0
            vehicle_heavy_a,opponent,Infinity,5,0
            """;
        var act = () => MatchCompositionCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*must be finite*");
    }

    [Fact]
    public void Composition_throws_when_zero_player_spawns_present()
    {
        const string csv = """
            tank_id,role,pos_x,pos_y,yaw_radians
            vehicle_heavy_a,opponent,5,5,0
            """;
        var act = () => MatchCompositionCsv.Parse(csv);
        act.Should().Throw<System.ArgumentException>().WithMessage("*exactly one Player spawn*");
    }

    [Fact]
    public void Composition_throws_when_more_than_one_player_present()
    {
        const string csv = """
            tank_id,role,pos_x,pos_y,yaw_radians
            vehicle_medium_a,player,0,0,0
            vehicle_medium_a,player,5,5,0
            """;
        var act = () => MatchCompositionCsv.Parse(csv);
        act.Should().Throw<System.ArgumentException>().WithMessage("*exactly one Player spawn*");
    }

    [Fact]
    public void LoadFile_throws_FileNotFoundException_for_missing_path()
    {
        var act = () => MatchCompositionCsv.LoadFile("nonexistent-match.csv");
        act.Should().Throw<FileNotFoundException>();
    }
}
