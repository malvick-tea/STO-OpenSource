using System.Collections.Generic;
using System.Linq;
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
using Opus.Net.Transport;
using Xunit;

namespace Garupan.Server.Match.Tests;

/// <summary>Multi-peer end-to-end coverage for <see cref="MatchHost"/> over a
/// <see cref="LoopbackTransportHub"/>. The 1:1 path is already pinned by
/// <c>MatchHostIntegrationTests</c>; the cases here exercise the N:1 paths that
/// <see cref="MatchHost"/>'s <c>BroadcastSnapshot</c> and per-peer input routing were
/// always built for but that <see cref="LoopbackTransportPair"/> could not surface.
/// <para>
/// All four peers spawn through one <see cref="MatchHost"/> instance; the test asserts
/// each peer's Welcome carries a distinct network id, snapshots fan out to every healthy
/// client, and per-peer input routes to the correct entity (assert via the post-tick
/// snapshot's entity-row positions).
/// </para></summary>
public sealed class MatchHostMultiPeerTests
{
    private const int PeerCount = 4;
    private const double OneTickDelta = 1.0 / 60.0;

    [Fact]
    public void Four_peers_each_receive_a_welcome_with_a_distinct_network_id()
    {
        using var harness = new MultiPeerHarness(PeerCount);

        harness.PumpOnce();

        harness.Host.PlayerCount.Should().Be(PeerCount);
        harness.Welcomes.Should().HaveCount(PeerCount);
        harness.Welcomes.Select(w => w.NetworkId).Should().OnlyHaveUniqueItems();
        harness.Host.LastBroadcastDeliveredCount.Should().Be(PeerCount);
    }

    [Fact]
    public void A_team_match_designates_exactly_one_commander_per_team()
    {
        using var harness = new MultiPeerHarness(PeerCount, MatchOutcomeRule.LastTeamStanding);

        harness.PumpOnce();

        var commanders = harness.Welcomes.Where(w => w.IsCommander).ToList();
        commanders.Should().HaveCount(2, "the first peer seated on each of the two teams commands it");
        commanders.Select(w => w.TeamId).Should().OnlyHaveUniqueItems("one commander per team");
    }

    [Fact]
    public void A_team_match_places_each_team_on_its_own_spawn_anchor()
    {
        // Tactical 5v5 alternates teams as peers join: 0 → PlayerSchool, 1 → OpponentSchool,
        // 2 → PlayerSchool, 3 → OpponentSchool. PlayerSchool slots stride from SpawnAnchor
        // (X = 0), OpponentSchool slots stride from the default-separation override anchor
        // (X = 200 m). The two lines open the round on opposite sides of the field.
        using var harness = new MultiPeerHarness(PeerCount, MatchOutcomeRule.LastTeamStanding);
        harness.PumpOnce();

        var snapshot = harness.SnapshotsByPeer[0][^1];
        var byId = snapshot.Entities.ToDictionary(e => (uint)e.Id);

        var playerSchoolFirst = byId[harness.Welcomes[0].NetworkId];
        var opponentSchoolFirst = byId[harness.Welcomes[1].NetworkId];

        harness.Welcomes[0].TeamId.Should().Be((byte)Team.PlayerSchool);
        harness.Welcomes[1].TeamId.Should().Be((byte)Team.OpponentSchool);

        playerSchoolFirst.Position.X.Should().Be(0f, "the first PlayerSchool peer sits at the configured anchor");
        opponentSchoolFirst.Position.X.Should().Be(
            MatchHostSpawnPlanner.DefaultTeamSeparationMeters,
            "the first OpponentSchool peer sits at the +200m default-separation anchor");
    }

    [Fact]
    public void A_team_match_opens_with_teams_facing_each_other()
    {
        using var harness = new MultiPeerHarness(PeerCount, MatchOutcomeRule.LastTeamStanding);
        harness.PumpOnce();

        var snapshot = harness.SnapshotsByPeer[0][^1];
        var byId = snapshot.Entities.ToDictionary(e => (uint)e.Id);

        var playerSchoolFirst = byId[harness.Welcomes[0].NetworkId];
        var opponentSchoolFirst = byId[harness.Welcomes[1].NetworkId];

        playerSchoolFirst.YawRadians.Should().Be(0f, "PlayerSchool tanks face +X");
        opponentSchoolFirst.YawRadians.Should().BeApproximately(
            System.MathF.PI, 1e-5f, "OpponentSchool tanks face back along -X");
    }

    [Fact]
    public void One_broadcast_fans_out_to_every_healthy_client()
    {
        using var harness = new MultiPeerHarness(PeerCount);

        harness.PumpOnce();

        foreach (var clientSnapshots in harness.SnapshotsByPeer)
        {
            clientSnapshots.Should().ContainSingle(
                "snapshot-interval-1 + healthy 4-peer hub means each peer receives every broadcast");
            clientSnapshots[0].Entities.Should().HaveCount(PeerCount);
        }
    }

    [Fact]
    public void Per_peer_inputs_route_to_distinct_entities_in_the_world()
    {
        using var harness = new MultiPeerHarness(PeerCount);
        harness.PumpOnce();

        // Peer 0: forward, no steering. Peer 1: backward. Peer 2: forward + right. Peer 3: idle.
        harness.Clients[0].SendInput(MakeInput(harness.Welcomes[0].NetworkId, throttle: 1f));
        harness.Clients[1].SendInput(MakeInput(harness.Welcomes[1].NetworkId, throttle: -1f));
        harness.Clients[2].SendInput(MakeInput(harness.Welcomes[2].NetworkId, throttle: 1f, steering: 1f));

        harness.PumpTicks(30);

        var lastSnapshot = harness.SnapshotsByPeer[0][^1];
        var byId = lastSnapshot.Entities.ToDictionary(e => (uint)e.Id);

        byId[harness.Welcomes[0].NetworkId].Position.X.Should().BeGreaterThan(0f, "peer 0 drove forward");
        byId[harness.Welcomes[1].NetworkId].Position.X.Should().BeLessThan(harness.SpawnXOf(1), "peer 1 drove backward");
        byId[harness.Welcomes[2].NetworkId].YawRadians.Should().NotBe(0f, "peer 2 steered right");
        byId[harness.Welcomes[3].NetworkId].Position.X.Should().Be(harness.SpawnXOf(3), "peer 3 stayed put");
    }

    [Fact]
    public void Disconnecting_one_peer_does_not_affect_the_others()
    {
        using var harness = new MultiPeerHarness(PeerCount);
        harness.PumpOnce();

        var droppedId = harness.HubPeerIds[2];
        harness.Hub.Disconnect(droppedId);
        harness.PumpTicks(1);

        harness.Host.PlayerCount.Should().Be(PeerCount - 1);
        harness.Host.LastBroadcastDeliveredCount.Should().Be(PeerCount - 1);

        // The surviving peers each received the second snapshot (post-disconnect).
        for (var i = 0; i < PeerCount; i++)
        {
            if (i == 2)
            {
                continue;
            }

            harness.SnapshotsByPeer[i]
                .Should().HaveCountGreaterThan(1, "surviving peers receive snapshots both before and after peer 2's disconnect");
            harness.SnapshotsByPeer[i][^1].Entities.Should().HaveCount(PeerCount - 1);
        }
    }

    private static ClientInputFrame MakeInput(uint networkId, float throttle = 0f, float steering = 0f) =>
        new(
            Tick: 1,
            NetworkId: networkId,
            Throttle: throttle,
            Steering: steering,
            TurretYawRadians: 0f,
            Flags: InputFlags.None);

    private sealed class MultiPeerHarness : System.IDisposable
    {
        private const float SpawnSpacing = 8f;

        public MultiPeerHarness(int peerCount, MatchOutcomeRule outcomeRule = MatchOutcomeRule.LastTankStanding)
        {
            Hub = LoopbackTransportHub.Create();
            Host = new MatchHost(Hub, new MatchHostOptions(
                PlayerSpec: TankRoster.VehicleMediumA,
                PlayerTeam: Team.PlayerSchool,
                SpawnAnchor: Vector2.Zero,
                SpawnSpacingMeters: SpawnSpacing,
                OutcomeRule: outcomeRule));

            Clients = new ClientSession[peerCount];
            HubPeerIds = new ConnectionId[peerCount];
            SnapshotsByPeer = new List<WorldSnapshot>[peerCount];

            for (var i = 0; i < peerCount; i++)
            {
                var connection = Hub.Accept($"loopback-hub-client-{i}");
                HubPeerIds[i] = connection.ServerSidePeerId;
                SnapshotsByPeer[i] = new List<WorldSnapshot>();
                var session = new ClientSession(connection.Client);
                var index = i;
                session.WelcomeReceived += w => Welcomes.Add(w);
                session.SnapshotReceived += s => SnapshotsByPeer[index].Add(s);
                Clients[i] = session;
            }
        }

        public LoopbackTransportHub Hub { get; }

        public MatchHost Host { get; }

        public ClientSession[] Clients { get; }

        public ConnectionId[] HubPeerIds { get; }

        public List<WorldSnapshot>[] SnapshotsByPeer { get; }

        public List<WelcomeFrame> Welcomes { get; } = new();

        public float SpawnXOf(int peerIndex) => peerIndex * SpawnSpacing;

        public void PumpOnce()
        {
            Host.Pump(OneTickDelta);
            foreach (var client in Clients)
            {
                client.Pump();
            }
        }

        public void PumpTicks(int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                PumpOnce();
            }
        }

        public void Dispose()
        {
            foreach (var client in Clients)
            {
                client.Dispose();
            }

            Host.Dispose();
            Hub.Dispose();
        }
    }
}
