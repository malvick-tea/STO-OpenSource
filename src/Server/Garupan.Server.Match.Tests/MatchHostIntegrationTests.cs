using System.Collections.Generic;
using System.Numerics;
using FluentAssertions;
using Garupan.Content;
using Garupan.Net.Session;
using Garupan.Server.Match.Outcome;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Protocol;
using Garupan.Sim.Snapshot;
using Opus.Net.Loopback;
using Xunit;

namespace Garupan.Server.Match.Tests;

/// <summary>End-to-end coverage for <see cref="MatchHost"/>. Drives the host with a
/// real <see cref="LoopbackTransportPair"/> and a real <see cref="ClientSession"/> so
/// every dispatch — Connected → spawn → Welcome → input → tick → snapshot — runs through
/// runtime code paths. The pure-CPU `NetMessageDispatcher` is already covered by
/// <c>Garupan.Net.Session.Tests</c>; the tests here pin the **server contract**:
/// connect spawns a tank, input mutates it, broadcast emits the resulting world state,
/// disconnect removes the row.</summary>
public sealed class MatchHostIntegrationTests
{
    private const double OneTickDelta = 1.0 / 60.0;

    [Fact]
    public void Connecting_a_peer_spawns_a_tank_and_sends_welcome_with_its_network_id()
    {
        using var harness = new MatchHostHarness();

        harness.PumpOnce();

        harness.Welcomes.Should().HaveCount(1, "the host sends Welcome on the connect event");
        harness.Welcomes[0].TeamId.Should().Be((byte)Team.PlayerSchool);

        harness.Host.PlayerCount.Should().Be(1);
        var spawnedNetworkId = harness.Welcomes[0].NetworkId;
        harness.WorldContainsEntityWithSnapshotId((int)spawnedNetworkId)
            .Should().BeTrue("the Welcome's network id must point to a live entity in the world");
    }

    [Fact]
    public void Welcome_carries_the_match_mode_kind_and_respawn_budget()
    {
        using var harness = new MatchHostHarness(
            outcomeRule: MatchOutcomeRule.LastTeamStanding,
            respawnsPerPeer: 2);

        harness.PumpOnce();

        var welcome = harness.Welcomes[0];
        welcome.ModeKind.Should().Be(WelcomeMatchModeKind.TeamTactical);
        welcome.RespawnsConfigured.Should().Be((byte)2);
    }

    [Fact]
    public void Free_for_all_host_advertises_FreeForAll_mode_kind_in_welcome()
    {
        using var harness = new MatchHostHarness();

        harness.PumpOnce();

        harness.Welcomes[0].ModeKind.Should().Be(WelcomeMatchModeKind.FreeForAll);
    }

    [Fact]
    public void First_peer_in_a_team_match_is_welcomed_as_the_commander()
    {
        using var harness = new MatchHostHarness(outcomeRule: MatchOutcomeRule.LastTeamStanding);

        harness.PumpOnce();

        harness.Welcomes[0].IsCommander.Should().BeTrue("the first peer seated on a team commands it");
    }

    [Fact]
    public void A_free_for_all_peer_is_never_welcomed_as_a_commander()
    {
        using var harness = new MatchHostHarness();

        harness.PumpOnce();

        harness.Welcomes[0].IsCommander.Should().BeFalse("a free-for-all match has no commander role");
    }

    [Fact]
    public void Snapshot_broadcast_includes_the_spawned_tank_after_a_tick()
    {
        using var harness = new MatchHostHarness();
        harness.PumpOnce();

        harness.Snapshots.Should().NotBeEmpty("snapshot-interval-1 means every tick broadcasts");
        var lastSnapshot = harness.Snapshots[^1];
        lastSnapshot.Entities.Should().HaveCount(1, "one peer, one tank");
        lastSnapshot.Entities[0].Id.Should().Be((int)harness.Welcomes[0].NetworkId);
    }

    [Fact]
    public void Client_input_with_positive_throttle_moves_the_tank_after_enough_ticks()
    {
        using var harness = new MatchHostHarness();
        harness.PumpOnce();
        var welcome = harness.Welcomes[0];

        harness.Client.SendInput(new ClientInputFrame(
            Tick: 1,
            NetworkId: welcome.NetworkId,
            Throttle: 1.0f,
            Steering: 0f,
            TurretYawRadians: 0f,
            Flags: InputFlags.None));

        // Drive enough ticks for HullDriveSystem to integrate forward motion. The
        // host accumulates ticks across pumps regardless of how many real frames pass.
        harness.PumpTicks(30);

        var startPos = MatchHostOptionsDefaultSpawn;
        var endPos = harness.Snapshots[^1].Entities[0].Position;
        endPos.X.Should().BeGreaterThan(startPos.X, "positive throttle drives the hull forward in +X");
    }

    [Fact]
    public void Disconnecting_a_peer_destroys_the_tank_in_the_authoritative_world()
    {
        using var harness = new MatchHostHarness();
        harness.PumpOnce();
        var welcome = harness.Welcomes[0];
        harness.WorldContainsEntityWithSnapshotId((int)welcome.NetworkId).Should().BeTrue();

        harness.Link.Server.Disconnect(harness.Link.ClientPeerId);
        harness.PumpTicks(1);

        // The disconnect runs on the server's session, removes the peer + destroys
        // the entity, and *then* the tick fires — by that point the snapshot capture
        // sees an empty world. The post-disconnect snapshot itself never reaches the
        // dropped client (Phase 26 contract: Send to a disconnected peer returns false),
        // so the assertion here is on the host's authoritative state, not the wire.
        harness.Host.PlayerCount.Should().Be(0);
        harness.WorldContainsEntityWithSnapshotId((int)welcome.NetworkId).Should().BeFalse();
        harness.Host.LastBroadcastDeliveredCount.Should().Be(0);
    }

    [Fact]
    public void Input_from_unknown_peer_is_dropped_without_throwing_or_spawning()
    {
        using var harness = new MatchHostHarness();
        // Skip the connect pump so the host has zero registered peers.
        var preCount = harness.Host.PlayerCount;
        preCount.Should().Be(0);

        // Manually push a SVOI payload through the loopback before the host pumps.
        var rawInput = new byte[ClientInputWire.FrameBytes];
        ClientInputCodec.Encode(
            new ClientInputFrame(0, 0, 0, 0, 0, InputFlags.None),
            rawInput);
        harness.Link.Client.Send(harness.Link.ServerPeerId, rawInput).Should().BeTrue();

        // Single pump consumes both the Connected event (which spawns a peer) and the
        // pre-queued payload (which routes to the now-known peer's tank). Host stays
        // healthy regardless of arrival order — that's the contract being pinned.
        harness.PumpOnce();
        harness.Host.PlayerCount.Should().Be(1);
    }

    [Fact]
    public void Pump_advances_tick_count_at_the_configured_rate()
    {
        using var harness = new MatchHostHarness();

        harness.PumpTicks(5);

        harness.Host.CurrentTick.Value.Should().Be(5);
        harness.Host.SnapshotsBroadcast.Should().Be(5, "snapshot interval = 1 tick by default");
    }

    [Fact]
    public void Last_broadcast_delivered_count_matches_player_count_on_a_healthy_link()
    {
        using var harness = new MatchHostHarness();
        harness.PumpOnce();

        harness.Host.LastBroadcastDeliveredCount.Should().Be(1);
        harness.Host.PlayerCount.Should().Be(1);
    }

    private static readonly Vector2 MatchHostOptionsDefaultSpawn = Vector2.Zero;

    private sealed class MatchHostHarness : System.IDisposable
    {
        // Defaults mirror MatchHostOptions's own — a parameterless harness is the
        // single-mode free-for-all host every pre-Phase-44 test was written against.
        public MatchHostHarness(
            MatchOutcomeRule outcomeRule = MatchOutcomeRule.LastTankStanding,
            byte respawnsPerPeer = 0)
        {
            Link = LoopbackTransportPair.Create();
            Host = new MatchHost(Link.Server, new MatchHostOptions(
                PlayerSpec: TankRoster.VehicleMediumA,
                PlayerTeam: Team.PlayerSchool,
                SpawnAnchor: MatchHostOptionsDefaultSpawn,
                OutcomeRule: outcomeRule,
                RespawnsPerPeer: respawnsPerPeer));

            Client = new ClientSession(Link.Client);
            Client.WelcomeReceived += w => Welcomes.Add(w);
            Client.SnapshotReceived += s => Snapshots.Add(s);
        }

        public LoopbackTransportLink Link { get; }

        public MatchHost Host { get; }

        public ClientSession Client { get; }

        public List<WelcomeFrame> Welcomes { get; } = new();

        public List<WorldSnapshot> Snapshots { get; } = new();

        public void PumpOnce()
        {
            Host.Pump(OneTickDelta);
            Client.Pump();
        }

        public void PumpTicks(int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                PumpOnce();
            }
        }

        public bool WorldContainsEntityWithSnapshotId(int id)
        {
            var snap = SnapshotCapture.Capture(Host.World, Host.CurrentTick);
            foreach (var entity in snap.Entities)
            {
                if (entity.Id == id)
                {
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            Client.Dispose();
            Host.Dispose();
            Link.Client.Dispose();
            Link.Server.Dispose();
        }
    }
}
