using FluentAssertions;
using Garupan.Sim.Components;
using Opus.Net.Transport;
using Xunit;

namespace Garupan.Server.Match.Tests;

/// <summary>Unit coverage for <see cref="MatchRoster"/> — the seated-peer set plus the
/// monotonic identity allocators. The headline contract is the never-reuse invariant:
/// <see cref="MatchRoster.Clear"/> drops the seating but must NOT rewind the network-id or
/// spawn-slot counters, so a recycled match cannot alias a fresh peer onto a departed
/// peer's identity.</summary>
public sealed class MatchRosterTests
{
    [Fact]
    public void Fresh_roster_is_empty()
    {
        var roster = new MatchRoster();

        roster.Count.Should().Be(0);
        roster.Players.Should().BeEmpty();
    }

    [Fact]
    public void Draw_spawn_index_returns_a_monotonic_sequence_from_zero()
    {
        var roster = new MatchRoster();

        roster.DrawSpawnIndex().Should().Be(0);
        roster.DrawSpawnIndex().Should().Be(1);
        roster.DrawSpawnIndex().Should().Be(2);
    }

    [Fact]
    public void Draw_network_id_returns_a_monotonic_sequence_from_one()
    {
        var roster = new MatchRoster();

        // Id 0 is reserved as the "not a networked entity" sentinel.
        roster.DrawNetworkId().Value.Should().Be(1u);
        roster.DrawNetworkId().Value.Should().Be(2u);
        roster.DrawNetworkId().Value.Should().Be(3u);
    }

    [Fact]
    public void Seat_then_TryGet_returns_the_seated_player()
    {
        var roster = new MatchRoster();
        var connection = new ConnectionId(7UL);

        roster.Seat(Player(connection, networkId: 1u));

        roster.TryGet(connection, out var seated).Should().BeTrue();
        seated.NetworkId.Should().Be(1u);
        roster.Count.Should().Be(1);
    }

    [Fact]
    public void TryGet_for_an_unseated_connection_returns_false()
    {
        var roster = new MatchRoster();

        roster.TryGet(new ConnectionId(99UL), out _).Should().BeFalse();
    }

    [Fact]
    public void Seating_the_same_connection_twice_overwrites_the_prior_row()
    {
        var roster = new MatchRoster();
        var connection = new ConnectionId(3UL);

        roster.Seat(Player(connection, networkId: 1u));
        roster.Seat(Player(connection, networkId: 2u));

        roster.Count.Should().Be(1, "the recycle path re-seats the same connection");
        roster.TryGet(connection, out var seated).Should().BeTrue();
        seated.NetworkId.Should().Be(2u);
    }

    [Fact]
    public void Remove_returns_and_drops_the_seated_player()
    {
        var roster = new MatchRoster();
        var connection = new ConnectionId(5UL);
        roster.Seat(Player(connection, networkId: 1u));

        roster.Remove(connection, out var removed).Should().BeTrue();
        removed.NetworkId.Should().Be(1u);
        roster.Count.Should().Be(0);
    }

    [Fact]
    public void Remove_for_an_unseated_connection_returns_false()
    {
        var roster = new MatchRoster();

        roster.Remove(new ConnectionId(42UL), out _).Should().BeFalse();
    }

    [Fact]
    public void Clear_empties_the_seating()
    {
        var roster = new MatchRoster();
        roster.Seat(Player(new ConnectionId(1UL), networkId: 1u));
        roster.Seat(Player(new ConnectionId(2UL), networkId: 2u));

        roster.Clear();

        roster.Count.Should().Be(0);
        roster.Players.Should().BeEmpty();
    }

    [Fact]
    public void Clear_does_not_rewind_the_identity_counters()
    {
        var roster = new MatchRoster();
        roster.DrawNetworkId().Value.Should().Be(1u);
        roster.DrawSpawnIndex().Should().Be(0);

        roster.Clear();

        // A recycled match must never re-issue a past identity.
        roster.DrawNetworkId().Value.Should().Be(2u);
        roster.DrawSpawnIndex().Should().Be(1);
    }

    [Fact]
    public void Players_reflects_every_seated_peer()
    {
        var roster = new MatchRoster();
        roster.Seat(Player(new ConnectionId(1UL), networkId: 1u));
        roster.Seat(Player(new ConnectionId(2UL), networkId: 2u));

        roster.Players.Should().HaveCount(2);
    }

    // The entity handle and spawn index are irrelevant to roster bookkeeping — the
    // roster keys on the connection and only carries the rest through.
    private static ConnectedPlayer Player(ConnectionId connection, uint networkId) =>
        new(connection, networkId, default, 0, Team.PlayerSchool);
}
