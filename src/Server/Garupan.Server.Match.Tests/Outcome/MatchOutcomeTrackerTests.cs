using System.Collections.Generic;
using FluentAssertions;
using Garupan.Server.Match.Outcome;
using Garupan.Sim.Components;
using Xunit;

namespace Garupan.Server.Match.Tests.Outcome;

/// <summary>
/// Headless coverage for <see cref="MatchOutcomeTracker"/> — the pure outcome-detection
/// state machine. Feeds it hand-built <see cref="MatchParticipant"/> rosters; pins the
/// last-tank-standing / last-team-standing rule arithmetic, the two-contender minimum,
/// and the decided-outcome latch.
/// </summary>
public sealed class MatchOutcomeTrackerTests
{
    [Fact]
    public void A_fresh_tracker_reports_an_in_progress_outcome()
    {
        var tracker = new MatchOutcomeTracker(MatchOutcomeRule.LastTankStanding);

        tracker.Current.IsDecided.Should().BeFalse();
        tracker.Current.Kind.Should().Be(MatchOutcomeKind.InProgress);
    }

    [Fact]
    public void A_lone_tank_can_never_win_the_free_for_all()
    {
        var tracker = new MatchOutcomeTracker(MatchOutcomeRule.LastTankStanding);

        tracker.Update(new[] { Alive(1) });

        tracker.Current.IsDecided.Should().BeFalse();
    }

    [Fact]
    public void Two_healthy_tanks_keep_the_free_for_all_in_progress()
    {
        var tracker = new MatchOutcomeTracker(MatchOutcomeRule.LastTankStanding);

        tracker.Update(new[] { Alive(1), Alive(2) });

        tracker.Current.Kind.Should().Be(MatchOutcomeKind.InProgress);
    }

    [Fact]
    public void The_last_tank_standing_wins_the_free_for_all()
    {
        var tracker = new MatchOutcomeTracker(MatchOutcomeRule.LastTankStanding);

        tracker.Update(new[] { KnockedOut(1), Alive(2) });

        tracker.Current.Kind.Should().Be(MatchOutcomeKind.Winner);
        tracker.Current.WinnerNetworkId.Should().Be(2u);
        tracker.Current.WinnerTeam.Should().Be(Team.None);
    }

    [Fact]
    public void A_three_tank_free_for_all_resolves_to_the_single_survivor()
    {
        var tracker = new MatchOutcomeTracker(MatchOutcomeRule.LastTankStanding);

        tracker.Update(new[] { KnockedOut(1), Alive(2), KnockedOut(3) });

        tracker.Current.Kind.Should().Be(MatchOutcomeKind.Winner);
        tracker.Current.WinnerNetworkId.Should().Be(2u);
    }

    [Fact]
    public void A_mutual_knock_out_ends_the_free_for_all_in_a_draw()
    {
        var tracker = new MatchOutcomeTracker(MatchOutcomeRule.LastTankStanding);

        tracker.Update(new[] { KnockedOut(1), KnockedOut(2) });

        tracker.Current.Kind.Should().Be(MatchOutcomeKind.Draw);
    }

    [Fact]
    public void A_decided_outcome_is_latched_against_a_later_roster_change()
    {
        var tracker = new MatchOutcomeTracker(MatchOutcomeRule.LastTankStanding);
        tracker.Update(new[] { KnockedOut(1), Alive(2) });

        // A straggler reviving / a fresh roster must not re-open the finished match.
        tracker.Update(new[] { Alive(1), Alive(2), Alive(3) });

        tracker.Current.Kind.Should().Be(MatchOutcomeKind.Winner);
        tracker.Current.WinnerNetworkId.Should().Be(2u);
    }

    [Fact]
    public void Disconnecting_after_a_free_for_all_was_contested_awards_the_survivor()
    {
        var tracker = new MatchOutcomeTracker(MatchOutcomeRule.LastTankStanding);
        tracker.Update(new[] { Alive(1), Alive(2) });

        tracker.Update(new[] { Alive(2) });

        tracker.Current.Kind.Should().Be(MatchOutcomeKind.Winner);
        tracker.Current.WinnerNetworkId.Should().Be(2u);
    }

    [Fact]
    public void Update_returns_the_current_latched_outcome()
    {
        var tracker = new MatchOutcomeTracker(MatchOutcomeRule.LastTankStanding);

        var returned = tracker.Update(new[] { KnockedOut(1), Alive(2) });

        returned.Should().Be(tracker.Current);
    }

    [Fact]
    public void A_two_team_match_stays_in_progress_while_both_teams_have_tanks()
    {
        var tracker = new MatchOutcomeTracker(MatchOutcomeRule.LastTeamStanding);

        tracker.Update(new[]
        {
            Alive(1, Team.PlayerSchool),
            Alive(2, Team.OpponentSchool),
        });

        tracker.Current.Kind.Should().Be(MatchOutcomeKind.InProgress);
    }

    [Fact]
    public void The_team_with_tanks_left_wins_the_team_match()
    {
        var tracker = new MatchOutcomeTracker(MatchOutcomeRule.LastTeamStanding);

        tracker.Update(new[]
        {
            Alive(1, Team.PlayerSchool),
            KnockedOut(2, Team.OpponentSchool),
            KnockedOut(3, Team.OpponentSchool),
        });

        tracker.Current.Kind.Should().Be(MatchOutcomeKind.Winner);
        tracker.Current.WinnerTeam.Should().Be(Team.PlayerSchool);
        tracker.Current.WinnerNetworkId.Should().Be(0u);
    }

    [Fact]
    public void A_single_team_roster_can_never_be_decided_by_team_elimination()
    {
        var tracker = new MatchOutcomeTracker(MatchOutcomeRule.LastTeamStanding);

        // Everyone on one team: last-team-standing has no opposing side to eliminate.
        tracker.Update(new[]
        {
            KnockedOut(1, Team.PlayerSchool),
            Alive(2, Team.PlayerSchool),
        });

        tracker.Current.Kind.Should().Be(MatchOutcomeKind.InProgress);
    }

    [Fact]
    public void Both_teams_wiped_ends_the_team_match_in_a_draw()
    {
        var tracker = new MatchOutcomeTracker(MatchOutcomeRule.LastTeamStanding);

        tracker.Update(new[]
        {
            KnockedOut(1, Team.PlayerSchool),
            KnockedOut(2, Team.OpponentSchool),
        });

        tracker.Current.Kind.Should().Be(MatchOutcomeKind.Draw);
    }

    [Fact]
    public void Disconnecting_a_team_after_the_match_was_contested_awards_the_remaining_team()
    {
        var tracker = new MatchOutcomeTracker(MatchOutcomeRule.LastTeamStanding);
        tracker.Update(new[]
        {
            Alive(1, Team.PlayerSchool),
            Alive(2, Team.OpponentSchool),
        });

        tracker.Update(new[] { Alive(1, Team.PlayerSchool) });

        tracker.Current.Kind.Should().Be(MatchOutcomeKind.Winner);
        tracker.Current.WinnerTeam.Should().Be(Team.PlayerSchool);
    }

    private static MatchParticipant Alive(uint networkId, Team team = Team.PlayerSchool) =>
        new(networkId, team, IsKnockedOut: false);

    private static MatchParticipant KnockedOut(uint networkId, Team team = Team.PlayerSchool) =>
        new(networkId, team, IsKnockedOut: true);
}
