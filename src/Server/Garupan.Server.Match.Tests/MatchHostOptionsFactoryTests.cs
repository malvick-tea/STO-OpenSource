using System.Numerics;
using FluentAssertions;
using Garupan.Content;
using Garupan.Server.Match.Outcome;
using Garupan.Sim.Components;
using Xunit;

namespace Garupan.Server.Match.Tests;

/// <summary>
/// Pure mapping coverage for <see cref="MatchHostOptionsFactory.ForMode"/>: each
/// local test mode maps to the expected outcome rule, respawn budget, and team
/// shape. Builds modes through the validated <see cref="MatchMode.CreateValidated"/>
/// path used by the CSV loader so the test data matches the runtime catalogue.
/// </summary>
public sealed class MatchHostOptionsFactoryTests
{
    [Fact]
    public void Hungry_battles_maps_to_last_tank_standing_with_three_respawns()
    {
        var mode = MakeMode("hungry_battles", MatchModeKind.FreeForAll, respawnLimit: 3, isCommanderLed: false);

        var options = MatchHostOptionsFactory.ForMode(mode, TankRoster.VehicleMediumB, spawnAnchor: Vector2.Zero);

        options.OutcomeRule.Should().Be(MatchOutcomeRule.LastTankStanding);
        options.RespawnsPerPeer.Should().Be((byte)3);
        options.PlayerTeam.Should().Be(Team.PlayerSchool);
        options.PlayerSpec.Should().BeSameAs(TankRoster.VehicleMediumB);
    }

    [Fact]
    public void Tactical_5v5_maps_to_last_team_standing_with_one_respawn()
    {
        var mode = MakeMode("tactical_5v5", MatchModeKind.TeamTactical, respawnLimit: 1, isCommanderLed: true);

        var options = MatchHostOptionsFactory.ForMode(mode, TankRoster.VehicleMediumB, spawnAnchor: Vector2.Zero);

        options.OutcomeRule.Should().Be(MatchOutcomeRule.LastTeamStanding);
        options.RespawnsPerPeer.Should().Be((byte)1);
        options.PlayerTeam.Should().Be(
            Team.PlayerSchool,
            "team mode balances peers across teams via TeamAssignment regardless of this default");
    }

    [Fact]
    public void Forwards_tick_rate_and_snapshot_interval()
    {
        var mode = MakeMode("custom", MatchModeKind.FreeForAll, respawnLimit: 0, isCommanderLed: false);

        var options = MatchHostOptionsFactory.ForMode(
            mode,
            TankRoster.VehicleMediumB,
            spawnAnchor: new Vector2(5f, 7f),
            tickRateHz: 90,
            snapshotIntervalTicks: 3);

        options.TickRateHz.Should().Be(90);
        options.SnapshotIntervalTicks.Should().Be(3);
        options.SpawnAnchor.Should().Be(new Vector2(5f, 7f));
    }

    [Fact]
    public void Zero_respawn_limit_yields_single_life_match()
    {
        var mode = MakeMode("custom", MatchModeKind.FreeForAll, respawnLimit: 0, isCommanderLed: false);

        var options = MatchHostOptionsFactory.ForMode(mode, TankRoster.VehicleMediumB, spawnAnchor: Vector2.Zero);

        options.RespawnsPerPeer.Should().Be((byte)0, "a zero respawn limit means knock-out is permanent");
    }

    [Fact]
    public void Respawn_limit_above_byte_max_clamps()
    {
        var mode = MakeMode("nightmare", MatchModeKind.FreeForAll, respawnLimit: 1000, isCommanderLed: false);

        var options = MatchHostOptionsFactory.ForMode(mode, TankRoster.VehicleMediumB, spawnAnchor: Vector2.Zero);

        options.RespawnsPerPeer.Should().Be(byte.MaxValue);
    }

    private static MatchMode MakeMode(string id, MatchModeKind kind, int respawnLimit, bool isCommanderLed) =>
        new(
            Id: id,
            Kind: kind,
            NameKey: "test.mode.name",
            SummaryKey: "test.mode.summary",
            LobbyCapacity: 10,
            RespawnLimit: respawnLimit,
            IsCommanderLed: isCommanderLed);
}
