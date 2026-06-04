using System;
using System.Numerics;
using FluentAssertions;
using Garupan.Content;
using Garupan.Server.Match.Outcome;
using Garupan.Sim.Components;
using Xunit;

namespace Garupan.Server.Match.Tests;

/// <summary>
/// Behavioural coverage for <see cref="MatchHostSpawnPlanner"/>'s pure spawn arithmetic:
/// per-team anchors, per-team yaw, the OpponentSpawnAnchor override + default-separation
/// fallback, and the team-count tally.
/// </summary>
public sealed class MatchHostSpawnPlannerTests
{
    [Fact]
    public void PlayerSchool_team_slot_zero_spawns_at_the_anchor()
    {
        var options = OptionsAt(new Vector2(5f, -3f));

        var spawn = MatchHostSpawnPlanner.ComputeSpawn(options, Team.PlayerSchool, teamSlot: 0);

        spawn.Should().Be(new Vector2(5f, -3f));
    }

    [Fact]
    public void PlayerSchool_team_slot_N_strides_in_positive_x()
    {
        var options = OptionsAt(Vector2.Zero, spacing: 4f);

        var spawn = MatchHostSpawnPlanner.ComputeSpawn(options, Team.PlayerSchool, teamSlot: 3);

        spawn.Should().Be(new Vector2(12f, 0f));
    }

    [Fact]
    public void OpponentSchool_falls_back_to_default_separation_when_no_override()
    {
        var options = OptionsAt(Vector2.Zero);

        var spawn = MatchHostSpawnPlanner.ComputeSpawn(options, Team.OpponentSchool, teamSlot: 0);

        spawn.X.Should().Be(
            MatchHostSpawnPlanner.DefaultTeamSeparationMeters,
            "the opponent team's default anchor sits +200m down +X — the local test 5v5 separation");
        spawn.Y.Should().Be(0f);
    }

    [Fact]
    public void OpponentSchool_override_wins_over_the_default_separation()
    {
        var options = OptionsAt(Vector2.Zero) with
        {
            OpponentSpawnAnchor = new Vector2(-150f, 30f),
        };

        var spawn = MatchHostSpawnPlanner.ComputeSpawn(options, Team.OpponentSchool, teamSlot: 1);

        spawn.Should().Be(new Vector2(-150f + options.SpawnSpacingMeters, 30f));
    }

    [Fact]
    public void PlayerSchool_ignores_the_opponent_anchor_override()
    {
        var options = OptionsAt(new Vector2(5f, 0f)) with
        {
            OpponentSpawnAnchor = new Vector2(-200f, 0f),
        };

        var spawn = MatchHostSpawnPlanner.ComputeSpawn(options, Team.PlayerSchool, teamSlot: 2);

        spawn.X.Should().Be(5f + (2 * options.SpawnSpacingMeters));
    }

    [Fact]
    public void Spawn_yaw_points_PlayerSchool_along_positive_x()
    {
        MatchHostSpawnPlanner.ComputeSpawnYaw(Team.PlayerSchool).Should().Be(0f);
    }

    [Fact]
    public void Spawn_yaw_points_OpponentSchool_back_along_negative_x()
    {
        MatchHostSpawnPlanner.ComputeSpawnYaw(Team.OpponentSchool).Should().BeApproximately(
            MathF.PI,
            1e-6f,
            "the two teams face each other across the X-axis so a Tactical 5v5 round opens nose-to-nose");
    }

    [Fact]
    public void CountTeams_groups_seated_players_by_team()
    {
        var players = new[]
        {
            Player(Team.PlayerSchool),
            Player(Team.PlayerSchool),
            Player(Team.OpponentSchool),
            Player(Team.None),
        };

        var (playerSchool, opponentSchool) = MatchHostSpawnPlanner.CountTeams(players);

        playerSchool.Should().Be(2);
        opponentSchool.Should().Be(1);
    }

    [Fact]
    public void CountTeams_on_empty_roster_returns_zeros()
    {
        var (playerSchool, opponentSchool) = MatchHostSpawnPlanner.CountTeams(Array.Empty<ConnectedPlayer>());

        playerSchool.Should().Be(0);
        opponentSchool.Should().Be(0);
    }

    [Fact]
    public void Default_team_separation_is_two_hundred_metres()
    {
        // Documented local test separation: ~200 m gives a Tactical 5v5 round-opening
        // approach phase before the first contact and keeps a clean spawn line.
        MatchHostSpawnPlanner.DefaultTeamSeparationMeters.Should().Be(200f);
    }

    private static MatchHostOptions OptionsAt(Vector2 spawn, float spacing = 4f) => new(
        PlayerSpec: TankRoster.VehicleMediumA,
        PlayerTeam: Team.PlayerSchool,
        SpawnAnchor: spawn,
        SpawnSpacingMeters: spacing);

    private static ConnectedPlayer Player(Team team) => new(
        Connection: default,
        NetworkId: 0u,
        Entity: default,
        SpawnIndex: 0,
        Team: team);
}
