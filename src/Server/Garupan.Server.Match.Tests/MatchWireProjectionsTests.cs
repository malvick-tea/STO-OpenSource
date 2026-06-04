using FluentAssertions;
using Garupan.Server.Match.Outcome;
using Garupan.Sim.Components;
using Garupan.Sim.Protocol;
using Xunit;

namespace Garupan.Server.Match.Tests;

/// <summary>Unit coverage for <see cref="MatchWireProjections"/> — the pure maps from the
/// server-tier match model onto the Sim wire frames the client consumes.</summary>
public sealed class MatchWireProjectionsTests
{
    [Fact]
    public void Team_standing_rule_projects_to_the_team_tactical_wire_mode()
    {
        MatchWireProjections.ToWelcomeModeKind(MatchOutcomeRule.LastTeamStanding)
            .Should().Be(WelcomeMatchModeKind.TeamTactical);
    }

    [Fact]
    public void Tank_standing_rule_projects_to_the_free_for_all_wire_mode()
    {
        MatchWireProjections.ToWelcomeModeKind(MatchOutcomeRule.LastTankStanding)
            .Should().Be(WelcomeMatchModeKind.FreeForAll);
    }

    [Fact]
    public void A_tank_winner_outcome_projects_to_a_winner_frame_carrying_the_network_id()
    {
        var frame = MatchWireProjections.ToMatchOverFrame(MatchOutcome.TankWinner(networkId: 7u));

        frame.Result.Should().Be(MatchOverResult.Winner);
        frame.WinnerNetworkId.Should().Be(7u);
    }

    [Fact]
    public void A_team_winner_outcome_projects_to_a_winner_frame_carrying_the_team()
    {
        var frame = MatchWireProjections.ToMatchOverFrame(MatchOutcome.TeamWinner(Team.OpponentSchool));

        frame.Result.Should().Be(MatchOverResult.Winner);
        frame.WinnerTeam.Should().Be((byte)Team.OpponentSchool);
    }

    [Fact]
    public void A_draw_outcome_projects_to_a_draw_frame_with_a_zeroed_winner()
    {
        var frame = MatchWireProjections.ToMatchOverFrame(MatchOutcome.Draw);

        frame.Result.Should().Be(MatchOverResult.Draw);
        frame.WinnerNetworkId.Should().Be(0u);
        frame.WinnerTeam.Should().Be((byte)0);
    }
}
