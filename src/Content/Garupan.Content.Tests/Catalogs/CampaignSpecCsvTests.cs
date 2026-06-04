using System.IO;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

/// <summary>Parser + cross-row validation coverage for <see cref="CampaignSpecCsv"/>.
/// The player commander canonical CSV at <c>data/campaigns/sample.csv</c> ships as the authoring
/// data file; the canonical-coverage test below also pins its shape.</summary>
public sealed class CampaignSpecCsvTests
{
    private const string Header = "id,title_key,episode,opponent,environment,objective,lore_key,briefing_key,script_id,node_x,node_y,prerequisites";

    private const string MetaId = "test_campaign";
    private const string MetaName = "campaign.test.name";
    private const string MetaSubtitle = "campaign.test.subtitle";

    [Fact]
    public void Parse_loads_a_single_well_formed_row()
    {
        var csv = $"""
            {Header}
            test.first,campaign.test.first.title,Episode 1,PlayerSchool,RuralOpen,Bracket,campaign.test.first.summary,campaign.test.first.briefing,scripted/first,0.1,0.5,
            """;
        var spec = CampaignSpecCsv.Parse(csv, MetaId, MetaName, MetaSubtitle);
        spec.Id.Should().Be(MetaId);
        spec.NameKey.Should().Be(MetaName);
        spec.Missions.Should().HaveCount(1);
        spec.Missions[0].Opponent.Should().Be(OpponentSchool.PlayerSchool);
        spec.Missions[0].Environment.Should().Be(MissionEnvironment.RuralOpen);
        spec.Missions[0].Objective.Should().Be(MissionObjective.Bracket);
        spec.Nodes[0].X.Should().Be(0.1f);
        spec.Nodes[0].Y.Should().Be(0.5f);
        spec.Nodes[0].Prerequisites.Should().BeEmpty();
    }

    [Fact]
    public void Parse_loads_pipe_separated_prerequisites()
    {
        var csv = $"""
            {Header}
            test.a,campaign.t.a.title,Episode 1,PlayerSchool,RuralOpen,Bracket,t.a.summary,t.a.briefing,scripted/a,0.1,0.5,
            test.b,campaign.t.b.title,Episode 2,RivalBravo,ForestedHills,KnockoutAll,t.b.summary,t.b.briefing,scripted/b,0.3,0.5,test.a
            test.c,campaign.t.c.title,Episode 3,RivalDelta,SnowyVillage,BreakOut,t.c.summary,t.c.briefing,scripted/c,0.6,0.5,test.a|test.b
            """;
        var spec = CampaignSpecCsv.Parse(csv, MetaId, MetaName, MetaSubtitle);
        spec.Nodes[2].Prerequisites.Should().BeEquivalentTo(new[] { "test.a", "test.b" });
    }

    [Fact]
    public void Parse_throws_on_dangling_prerequisite_reference()
    {
        var csv = $"""
            {Header}
            test.a,t.a.title,Episode 1,PlayerSchool,RuralOpen,Bracket,t.a.summary,t.a.briefing,scripted/a,0.1,0.5,unknown_mission
            """;
        var act = () => CampaignSpecCsv.Parse(csv, MetaId, MetaName, MetaSubtitle);
        act.Should().Throw<InvalidDataException>().WithMessage("*unknown_mission*does not match*");
    }

    [Fact]
    public void Parse_throws_on_self_referential_prerequisite()
    {
        var csv = $"""
            {Header}
            test.a,t.a.title,Episode 1,PlayerSchool,RuralOpen,Bracket,t.a.summary,t.a.briefing,scripted/a,0.1,0.5,test.a
            """;
        var act = () => CampaignSpecCsv.Parse(csv, MetaId, MetaName, MetaSubtitle);
        act.Should().Throw<InvalidDataException>().WithMessage("*lists itself as a prerequisite*");
    }

    [Fact]
    public void Parse_throws_on_duplicate_mission_id()
    {
        var csv = $"""
            {Header}
            test.a,t.a.title,Episode 1,PlayerSchool,RuralOpen,Bracket,t.a.summary,t.a.briefing,scripted/a,0.1,0.5,
            test.a,t.a.title,Episode 2,RivalBravo,RuralOpen,KnockoutAll,t.a.summary,t.a.briefing,scripted/a,0.3,0.5,
            """;
        var act = () => CampaignSpecCsv.Parse(csv, MetaId, MetaName, MetaSubtitle);
        act.Should().Throw<InvalidDataException>().WithMessage("*appears more than once*");
    }

    [Fact]
    public void Parse_throws_on_unknown_opponent_school()
    {
        var csv = $"""
            {Header}
            test.a,t.a.title,Episode 1,Atlantis,RuralOpen,Bracket,t.a.summary,t.a.briefing,scripted/a,0.1,0.5,
            """;
        var act = () => CampaignSpecCsv.Parse(csv, MetaId, MetaName, MetaSubtitle);
        act.Should().Throw<InvalidDataException>().WithMessage("*opponent*Atlantis*not a valid*");
    }

    [Fact]
    public void Parse_throws_on_unknown_environment()
    {
        var csv = $"""
            {Header}
            test.a,t.a.title,Episode 1,PlayerSchool,DeepSpace,Bracket,t.a.summary,t.a.briefing,scripted/a,0.1,0.5,
            """;
        var act = () => CampaignSpecCsv.Parse(csv, MetaId, MetaName, MetaSubtitle);
        act.Should().Throw<InvalidDataException>().WithMessage("*environment*DeepSpace*");
    }

    [Fact]
    public void Parse_throws_on_unknown_objective()
    {
        var csv = $"""
            {Header}
            test.a,t.a.title,Episode 1,PlayerSchool,RuralOpen,WinForever,t.a.summary,t.a.briefing,scripted/a,0.1,0.5,
            """;
        var act = () => CampaignSpecCsv.Parse(csv, MetaId, MetaName, MetaSubtitle);
        act.Should().Throw<InvalidDataException>().WithMessage("*objective*WinForever*");
    }

    [Fact]
    public void Parse_throws_on_empty_id_column()
    {
        var csv = $"""
            {Header}
            ,t.a.title,Episode 1,PlayerSchool,RuralOpen,Bracket,t.a.summary,t.a.briefing,scripted/a,0.1,0.5,
            """;
        var act = () => CampaignSpecCsv.Parse(csv, MetaId, MetaName, MetaSubtitle);
        act.Should().Throw<InvalidDataException>().WithMessage("*id*empty*");
    }

    [Fact]
    public void Parse_throws_on_header_mismatch()
    {
        var csv = """
            id,title,opponent,env,obj,summary,briefing,script,x,y,prereq
            test.a,t.a.title,PlayerSchool,RuralOpen,Bracket,t.a.summary,t.a.briefing,scripted/a,0.1,0.5,
            """;
        var act = () => CampaignSpecCsv.Parse(csv, MetaId, MetaName, MetaSubtitle);
        act.Should().Throw<InvalidDataException>().WithMessage("*header mismatch*");
    }

    [Fact]
    public void Parse_throws_on_non_finite_node_coordinate()
    {
        var csv = $"""
            {Header}
            test.a,t.a.title,Episode 1,PlayerSchool,RuralOpen,Bracket,t.a.summary,t.a.briefing,scripted/a,NaN,0.5,
            """;
        var act = () => CampaignSpecCsv.Parse(csv, MetaId, MetaName, MetaSubtitle);
        act.Should().Throw<InvalidDataException>().WithMessage("*must be finite*");
    }

    [Fact]
    public void Parse_supports_lowercase_enum_names()
    {
        var csv = $"""
            {Header}
            test.a,t.a.title,Episode 1,rival_bravo,forestedhills,knockoutall,t.a.summary,t.a.briefing,scripted/a,0.1,0.5,
            """;
        var spec = CampaignSpecCsv.Parse(csv, MetaId, MetaName, MetaSubtitle);
        spec.Missions[0].Opponent.Should().Be(OpponentSchool.RivalBravo);
        spec.Missions[0].Environment.Should().Be(MissionEnvironment.ForestedHills);
        spec.Missions[0].Objective.Should().Be(MissionObjective.KnockoutAll);
    }

    [Fact]
    public void Parse_throws_when_csv_has_only_a_header()
    {
        var act = () => CampaignSpecCsv.Parse(Header, MetaId, MetaName, MetaSubtitle);
        act.Should().Throw<InvalidDataException>().WithMessage("*at least a header*");
    }

    [Fact]
    public void LoadFile_throws_FileNotFoundException_for_missing_path()
    {
        var act = () => CampaignSpecCsv.LoadFile("nonexistent.csv", MetaId, MetaName, MetaSubtitle);
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Sample_campaign_csv_loads_with_six_missions_in_source_order()
    {
        var canonicalCsvPath = Path.Combine(
            System.AppContext.BaseDirectory, "data", "campaigns", "sample.csv");
        var spec = CampaignSpecCsv.LoadFile(
            canonicalCsvPath,
            SampleCampaign.Id,
            SampleCampaign.NameKey,
            SampleCampaign.ShortDescriptionKey);

        spec.Id.Should().Be(SampleCampaign.Id);
        spec.Missions.Should().HaveCount(6);
        spec.Missions[0].Id.Should().Be("sample.prefectural");
        spec.Missions[1].Id.Should().Be("sample.rival_alpha");
        spec.Missions[2].Id.Should().Be("sample.rival_bravo");
        spec.Missions[3].Id.Should().Be("sample.rival_charlie");
        spec.Missions[4].Id.Should().Be("sample.rival_delta");
        spec.Missions[5].Id.Should().Be("sample.rival_echo");
        spec.Missions[5].Opponent.Should().Be(OpponentSchool.RivalEcho);
        spec.Nodes[5].Prerequisites.Should().BeEquivalentTo(new[] { "sample.rival_delta" });
    }
}
