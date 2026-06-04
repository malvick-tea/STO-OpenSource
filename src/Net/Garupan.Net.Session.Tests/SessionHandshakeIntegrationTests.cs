using System;
using System.Collections.Generic;
using System.Numerics;
using FluentAssertions;
using Garupan.Sim.Components;
using Garupan.Sim.Protocol;
using Garupan.Sim.Snapshot;
using Opus.Foundation;
using Opus.Net.Loopback;
using Opus.Net.Transport;
using Xunit;

namespace Garupan.Net.Session.Tests;

/// <summary>End-to-end coverage for <see cref="ClientSession"/> + <see cref="ServerSession"/>
/// over a real <see cref="LoopbackTransportPair"/>. The transport's own behaviour is
/// covered by <c>Opus.Net.Tests.LoopbackTransportTests</c>; the tests here pin the
/// **session** contract: the right codec runs on the right side, frames travel both
/// directions, disconnect propagates, malformed bytes are dropped without aborting the
/// session.</summary>
public sealed class SessionHandshakeIntegrationTests
{
    [Fact]
    public void Initial_pump_surfaces_connected_on_both_sides()
    {
        using var harness = new SessionHarness();

        harness.PumpBoth();

        harness.ClientConnected.Should().Be(harness.Link.ServerPeerId);
        harness.Client.IsConnected.Should().BeTrue();
        harness.Client.ServerPeer.Should().Be(harness.Link.ServerPeerId);
        harness.ServerConnectedPeers.Should().Contain(harness.Link.ClientPeerId);
    }

    [Fact]
    public void Server_sends_welcome_after_client_connects_and_client_receives_decoded_frame()
    {
        using var harness = new SessionHarness();
        harness.PumpBoth();

        var welcome = new WelcomeFrame(
            NetworkId: 42u, TeamId: 7, WelcomeMatchModeKind.TeamTactical, RespawnsConfigured: 1, IsCommander: true);
        harness.Server.SendWelcome(harness.Link.ClientPeerId, welcome).Should().BeTrue();

        harness.Client.Pump();

        harness.WelcomesReceived.Should().ContainSingle().Which.Should().Be(welcome);
    }

    [Fact]
    public void Client_sends_input_and_server_dispatches_decoded_frame_to_the_correct_peer()
    {
        using var harness = new SessionHarness();
        harness.PumpBoth();

        var frame = new ClientInputFrame(
            Tick: 1234,
            NetworkId: 7,
            Throttle: 0.5f,
            Steering: -0.25f,
            TurretYawRadians: 1.5f,
            Flags: InputFlags.Fire);
        harness.Client.SendInput(frame).Should().BeTrue();

        harness.Server.Pump();

        harness.ClientInputs.Should().HaveCount(1);
        harness.ClientInputs[0].Peer.Should().Be(harness.Link.ClientPeerId);
        harness.ClientInputs[0].Frame.Should().Be(frame);
    }

    [Fact]
    public void Server_broadcasts_snapshot_and_client_receives_it_decoded()
    {
        using var harness = new SessionHarness();
        harness.PumpBoth();

        var snap = new WorldSnapshot(
            new Tick(99),
            new[]
            {
                new EntitySnapshot(
                    Id: 5,
                    Position: new Vector2(1.0f, 2.0f),
                    YawRadians: 0.5f,
                    TurretYawRadians: 1.5f,
                    StateFlags: EntityStateFlags.None),
            },
            new[]
            {
                new ProjectileSnapshot(
                    Id: 11,
                    Position: new Vector2(3.0f, 4.0f),
                    Velocity: new Vector2(0.1f, 0.2f),
                    Family: AmmoType.AP),
            });

        harness.Server.BroadcastSnapshot(snap).Should().Be(1);
        harness.Client.Pump();

        harness.SnapshotsReceived.Should().ContainSingle();
        harness.SnapshotsReceived[0].Tick.Should().Be(snap.Tick);
        harness.SnapshotsReceived[0].Entities.Should().HaveCount(1);
        harness.SnapshotsReceived[0].Projectiles.Should().HaveCount(1);
    }

    [Fact]
    public void Server_broadcasts_match_over_and_client_receives_the_decoded_verdict()
    {
        using var harness = new SessionHarness();
        harness.PumpBoth();

        var verdict = new MatchOverFrame(MatchOverResult.Winner, WinnerNetworkId: 7u, WinnerTeam: 2);
        harness.Server.BroadcastMatchOver(verdict).Should().Be(1);
        harness.Client.Pump();

        harness.MatchOversReceived.Should().ContainSingle().Which.Should().Be(verdict);
    }

    [Fact]
    public void Disconnect_on_one_side_propagates_to_both_sessions()
    {
        using var harness = new SessionHarness();
        harness.PumpBoth();

        harness.Link.Server.Disconnect(harness.Link.ClientPeerId);
        harness.PumpBoth();

        harness.Client.IsConnected.Should().BeFalse();
        harness.ClientDisconnects.Should().HaveCount(1);
        harness.ServerDisconnects.Should().HaveCount(1);
        harness.ServerConnectedPeers.Should().BeEmpty();
    }

    [Fact]
    public void Send_input_before_connected_returns_false_and_does_nothing()
    {
        using var harness = new SessionHarness();

        var frame = new ClientInputFrame(0, 0, 0, 0, 0, InputFlags.None);
        harness.Client.SendInput(frame).Should().BeFalse();

        // Server still pumps the queued Connected event normally; the failed send must
        // not have polluted the transport's mailbox with a ghost payload.
        harness.PumpBoth();
        harness.ClientInputs.Should().BeEmpty();
    }

    [Fact]
    public void Server_logs_and_drops_a_corrupt_payload_without_aborting()
    {
        using var harness = new SessionHarness();
        harness.PumpBoth();

        // SVOI magic + truncated body — passes Classify but fails decode.
        var bad = new byte[ClientInputWire.HeaderBytes];
        ClientInputWire.Magic.AsSpan().CopyTo(bad);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(4), ProtocolVersion.Wire);
        harness.Link.Client.Send(harness.Link.ServerPeerId, bad).Should().BeTrue();

        harness.Server.Pump();

        harness.ClientInputs.Should().BeEmpty();
        harness.Server.ConnectedPeers.Should().ContainSingle();
    }

    [Fact]
    public void Server_ignores_a_welcome_payload_from_a_client_wrong_direction()
    {
        using var harness = new SessionHarness();
        harness.PumpBoth();

        var welcomeBytes = new byte[WelcomeWire.FrameBytes];
        WelcomeCodec.Encode(new WelcomeFrame(0u, 0, WelcomeMatchModeKind.FreeForAll, 0, false), welcomeBytes);
        harness.Link.Client.Send(harness.Link.ServerPeerId, welcomeBytes).Should().BeTrue();

        harness.Server.Pump();

        // The wrong-direction frame is logged + dropped; no input handler fires.
        harness.ClientInputs.Should().BeEmpty();
    }

    [Fact]
    public void Server_ignores_a_match_over_payload_from_a_client_wrong_direction()
    {
        using var harness = new SessionHarness();
        harness.PumpBoth();

        var matchOverBytes = new byte[MatchOverWire.FrameBytes];
        MatchOverCodec.Encode(new MatchOverFrame(MatchOverResult.Draw, 0u, 0), matchOverBytes);
        harness.Link.Client.Send(harness.Link.ServerPeerId, matchOverBytes).Should().BeTrue();

        harness.Server.Pump();

        // A match-over frame is server → client only; the server logs + drops it.
        harness.ClientInputs.Should().BeEmpty();
        harness.Server.ConnectedPeers.Should().ContainSingle();
    }

    [Fact]
    public void Repeated_pump_with_no_events_is_idempotent()
    {
        using var harness = new SessionHarness();
        harness.PumpBoth();

        var initialConnects = harness.ClientConnectedCount;
        harness.PumpBoth();
        harness.PumpBoth();

        harness.ClientConnectedCount.Should().Be(initialConnects);
    }

    private sealed class SessionHarness : IDisposable
    {
        public SessionHarness()
        {
            Link = LoopbackTransportPair.Create();
            Client = new ClientSession(Link.Client);
            Server = new ServerSession(Link.Server);

            Client.Connected += OnClientConnected;
            Client.Disconnected += peer => ClientDisconnects.Add(peer);
            Client.WelcomeReceived += w => WelcomesReceived.Add(w);
            Client.SnapshotReceived += s => SnapshotsReceived.Add(s);
            Client.MatchOverReceived += m => MatchOversReceived.Add(m);

            Server.PeerConnected += peer => ServerConnects.Add(peer);
            Server.PeerDisconnected += peer => ServerDisconnects.Add(peer);
            Server.ClientInputReceived += (peer, frame) => ClientInputs.Add(new PeerInput(peer, frame));
        }

        public LoopbackTransportLink Link { get; }

        public ClientSession Client { get; }

        public ServerSession Server { get; }

        public ConnectionId ClientConnected { get; private set; } = ConnectionId.None;

        public int ClientConnectedCount { get; private set; }

        public List<ConnectionId> ClientDisconnects { get; } = new();

        public List<ConnectionId> ServerConnects { get; } = new();

        public List<ConnectionId> ServerDisconnects { get; } = new();

        public List<WelcomeFrame> WelcomesReceived { get; } = new();

        public List<WorldSnapshot> SnapshotsReceived { get; } = new();

        public List<MatchOverFrame> MatchOversReceived { get; } = new();

        public List<PeerInput> ClientInputs { get; } = new();

        public IReadOnlyCollection<ConnectionId> ServerConnectedPeers => Server.ConnectedPeers;

        public void PumpBoth()
        {
            Client.Pump();
            Server.Pump();
        }

        public void Dispose()
        {
            Client.Dispose();
            Server.Dispose();
            Link.Client.Dispose();
            Link.Server.Dispose();
        }

        private void OnClientConnected(ConnectionId peer)
        {
            ClientConnected = peer;
            ClientConnectedCount++;
        }
    }

    private readonly record struct PeerInput(ConnectionId Peer, ClientInputFrame Frame);
}
