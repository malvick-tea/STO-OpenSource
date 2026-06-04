using FluentAssertions;
using Garupan.Server.Match.Outcome;
using Garupan.Sim.Components;
using Xunit;

namespace Garupan.Server.Match.Tests;

/// <summary>Unit coverage for <see cref="TeamAssignment"/> — the pure rules that seat the
/// next peer joining a match: which <see cref="Team"/> it lands on and whether it holds
/// the commander role.</summary>
public sealed class TeamAssignmentTests
{
    [Fact]
    public void NextTeam_in_a_free_for_all_always_returns_the_configured_team()
    {
        TeamAssignment.NextTeam(MatchOutcomeRule.LastTankStanding, Team.PlayerSchool, 3, 1)
            .Should().Be(Team.PlayerSchool);
    }

    [Fact]
    public void NextTeam_in_a_team_match_fills_the_lighter_team()
    {
        TeamAssignment.NextTeam(MatchOutcomeRule.LastTeamStanding, Team.PlayerSchool, 2, 1)
            .Should().Be(Team.OpponentSchool);
    }

    [Fact]
    public void NextTeam_in_a_team_match_breaks_a_tie_toward_the_player_school()
    {
        TeamAssignment.NextTeam(MatchOutcomeRule.LastTeamStanding, Team.PlayerSchool, 2, 2)
            .Should().Be(Team.PlayerSchool);
    }

    [Fact]
    public void IsCommander_is_false_in_a_free_for_all_match()
    {
        TeamAssignment.IsCommander(MatchOutcomeRule.LastTankStanding, Team.PlayerSchool, 0, 0)
            .Should().BeFalse("a free-for-all has no teams and so no commander role");
    }

    [Fact]
    public void IsCommander_is_true_for_the_first_peer_on_the_player_school()
    {
        TeamAssignment.IsCommander(MatchOutcomeRule.LastTeamStanding, Team.PlayerSchool, 0, 0)
            .Should().BeTrue();
    }

    [Fact]
    public void IsCommander_is_true_for_the_first_peer_on_the_opponent_school()
    {
        TeamAssignment.IsCommander(MatchOutcomeRule.LastTeamStanding, Team.OpponentSchool, 1, 0)
            .Should().BeTrue("no peer is seated on the opponent school yet");
    }

    [Fact]
    public void IsCommander_is_false_for_a_later_peer_on_the_player_school()
    {
        TeamAssignment.IsCommander(MatchOutcomeRule.LastTeamStanding, Team.PlayerSchool, 1, 0)
            .Should().BeFalse("the player school already has a commander");
    }

    [Fact]
    public void IsCommander_is_false_for_a_later_peer_on_the_opponent_school()
    {
        TeamAssignment.IsCommander(MatchOutcomeRule.LastTeamStanding, Team.OpponentSchool, 1, 2)
            .Should().BeFalse("the opponent school already has a commander");
    }
}
