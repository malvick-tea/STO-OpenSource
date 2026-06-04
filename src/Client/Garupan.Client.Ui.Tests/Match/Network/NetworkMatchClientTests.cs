using System;
using System.Collections.Generic;
using System.Numerics;
using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Garupan.Net.Session;
using Garupan.Sim.Components;
using Garupan.Sim.Protocol;
using Garupan.Sim.Snapshot;
using Opus.Foundation;
using Opus.Net.Loopback;
using Opus.Net.Transport;
using Xunit;

namespace Garupan.Client.Ui.Tests.Match.Network;

/// <summary>
/// End-to-end coverage for <see cref="NetworkMatchClient"/> over a real
/// <see cref="LoopbackTransportPair"/> driven by a runtime <see cref="ServerSession"/>
/// on the far side — no mocks. Pins the connect → welcome → snapshot → input → drop
/// lifecycle the lobby and the network match screen depend on.
/// </summary>
public sealed class NetworkMatchClientTests
{
    private static readonly ClientInputFrame NeutralFrame =
        new(Tick: 0, NetworkId: 0u, Throttle: 0f, Steering: 0f, TurretYawRadians: 0f, Flags: InputFlags.None);

    [Fact]
    public void New_client_starts_connecting_with_no_identity_and_no_snapshot()
    {
        using var harness = new Harness();

        harness.Client.State.Should().Be(NetworkMatchConnectionState.Connecting);
        harness.Client.LocalNetworkId.Should().Be(0u);
        harness.Client.LatestSnapshot.Should().BeNull();
        harness.Client.SnapshotsReceived.Should().Be(0);
    }

    [Fact]
    public void Welcome_flips_the_client_to_connected_and_exposes_the_local_identity()
    {
        using var harness = new Harness();

        harness.ConnectAndWelcome(networkId: 9u, teamId: 2);

        harness.Client.State.Should().Be(NetworkMatchConnectionState.Connected);
        harness.Client.LocalNetworkId.Should().Be(9u);
        harness.Client.LocalTeamId.Should().Be((byte)2);
    }

    [Fact]
    public void Welcome_surfaces_the_match_mode_and_respawn_budget()
    {
        using var harness = new Harness();
        harness.PumpBoth();

        harness.Server.SendWelcome(
            harness.Link.ClientPeerId,
            new WelcomeFrame(
                NetworkId: 5u, TeamId: 1, WelcomeMatchModeKind.TeamTactical, RespawnsConfigured: 1, IsCommander: false));
        harness.Client.Pump();

        harness.Client.MatchModeKind.Should().Be(WelcomeMatchModeKind.TeamTactical);
        harness.Client.MatchRespawnsConfigured.Should().Be((byte)1);
    }

    [Fact]
    public void Welcome_surfaces_the_commander_flag()
    {
        using var harness = new Harness();
        harness.PumpBoth();

        harness.Server.SendWelcome(
            harness.Link.ClientPeerId,
            new WelcomeFrame(
                NetworkId: 5u, TeamId: 1, WelcomeMatchModeKind.TeamTactical, RespawnsConfigured: 1, IsCommander: true));
        harness.Client.Pump();

        harness.Client.IsCommander.Should().BeTrue();
    }

    [Fact]
    public void Snapshot_broadcasts_surface_as_the_latest_snapshot_and_bump_the_counter()
    {
        using var harness = new Harness();
        harness.ConnectAndWelcome(networkId: 1u, teamId: 0);

        harness.Server.BroadcastSnapshot(SnapshotAtTick(10));
        harness.Client.Pump();
        harness.Server.BroadcastSnapshot(SnapshotAtTick(11));
        harness.Client.Pump();

        harness.Client.SnapshotsReceived.Should().Be(2);
        harness.Client.LatestSnapshot.Should().NotBeNull();
        harness.Client.LatestSnapshot!.Tick.Should().Be(new Tick(11));
    }

    [Fact]
    public void A_connected_client_has_no_verdict_before_the_match_over_frame()
    {
        using var harness = new Harness();
        harness.ConnectAndWelcome(networkId: 5u, teamId: 0);

        harness.Client.IsMatchOver.Should().BeFalse();
        harness.Client.MatchOver.Should().BeNull();
        harness.Client.Verdict.Should().BeNull();
    }

    [Fact]
    public void A_match_over_broadcast_surfaces_on_the_client()
    {
        using var harness = new Harness();
        harness.ConnectAndWelcome(networkId: 5u, teamId: 0);

        var frame = new MatchOverFrame(MatchOverResult.Winner, WinnerNetworkId: 5u, WinnerTeam: 0);
        harness.Server.BroadcastMatchOver(frame).Should().Be(1);
        harness.Client.Pump();

        harness.Client.IsMatchOver.Should().BeTrue();
        harness.Client.MatchOver.Should().Be(frame);
    }

    [Fact]
    public void Verdict_is_victory_when_the_local_tank_wins_the_free_for_all()
    {
        using var harness = new Harness();
        harness.ConnectAndWelcome(networkId: 5u, teamId: 0);

        harness.Server.BroadcastMatchOver(new MatchOverFrame(MatchOverResult.Winner, WinnerNetworkId: 5u, WinnerTeam: 0));
        harness.Client.Pump();

        harness.Client.Verdict.Should().Be(NetworkMatchVerdict.Victory);
    }

    [Fact]
    public void Verdict_is_defeat_when_another_tank_wins_the_free_for_all()
    {
        using var harness = new Harness();
        harness.ConnectAndWelcome(networkId: 5u, teamId: 0);

        harness.Server.BroadcastMatchOver(new MatchOverFrame(MatchOverResult.Winner, WinnerNetworkId: 9u, WinnerTeam: 0));
        harness.Client.Pump();

        harness.Client.Verdict.Should().Be(NetworkMatchVerdict.Defeat);
    }

    [Fact]
    public void Verdict_is_draw_when_the_match_ends_with_no_winner()
    {
        using var harness = new Harness();
        harness.ConnectAndWelcome(networkId: 5u, teamId: 0);

        harness.Server.BroadcastMatchOver(new MatchOverFrame(MatchOverResult.Draw, WinnerNetworkId: 0u, WinnerTeam: 0));
        harness.Client.Pump();

        harness.Client.Verdict.Should().Be(NetworkMatchVerdict.Draw);
    }

    [Fact]
    public void Verdict_is_victory_when_the_local_team_wins()
    {
        using var harness = new Harness();
        harness.ConnectAndWelcome(networkId: 5u, teamId: 2);

        harness.Server.BroadcastMatchOver(new MatchOverFrame(MatchOverResult.Winner, WinnerNetworkId: 0u, WinnerTeam: 2));
        harness.Client.Pump();

        harness.Client.Verdict.Should().Be(NetworkMatchVerdict.Victory);
    }

    [Fact]
    public void Verdict_is_defeat_when_another_team_wins()
    {
        using var harness = new Harness();
        harness.ConnectAndWelcome(networkId: 5u, teamId: 2);

        harness.Server.BroadcastMatchOver(new MatchOverFrame(MatchOverResult.Winner, WinnerNetworkId: 0u, WinnerTeam: 1));
        harness.Client.Pump();

        harness.Client.Verdict.Should().Be(NetworkMatchVerdict.Defeat);
    }

    [Fact]
    public void A_second_welcome_clears_the_prior_match_over_state_and_snapshot()
    {
        using var harness = new Harness();
        harness.ConnectAndWelcome(networkId: 5u, teamId: 0);
        harness.Server.BroadcastSnapshot(SnapshotAtTick(11));
        harness.Server.BroadcastMatchOver(
            new MatchOverFrame(MatchOverResult.Winner, WinnerNetworkId: 5u, WinnerTeam: 0));
        harness.Client.Pump();
        harness.Client.IsMatchOver.Should().BeTrue();
        harness.Client.LatestSnapshot.Should().NotBeNull();

        // The server recycles the match for the next round — a fresh Welcome arrives with
        // a new local NetworkId. The client treats this as a "new match" boundary and
        // clears the prior terminal-banner state + the stale snapshot.
        harness.Server.SendWelcome(
            harness.Link.ClientPeerId,
            new WelcomeFrame(
                NetworkId: 12u, TeamId: 1, WelcomeMatchModeKind.TeamTactical, RespawnsConfigured: 1, IsCommander: false));
        harness.Client.Pump();

        harness.Client.LocalNetworkId.Should().Be(12u);
        harness.Client.LocalTeamId.Should().Be((byte)1);
        harness.Client.IsMatchOver.Should().BeFalse("a new Welcome means a new match");
        harness.Client.MatchOver.Should().BeNull();
        harness.Client.Verdict.Should().BeNull();
        harness.Client.LatestSnapshot.Should().BeNull("the stale snapshot belongs to the prior match");
    }

    [Fact]
    public void Send_input_before_welcome_returns_false()
    {
        using var harness = new Harness();
        harness.PumpBoth(); // transport-level Connected surfaced, but no Welcome yet

        harness.Client.SendInput(NeutralFrame).Should().BeFalse();
    }

    [Fact]
    public void Send_input_after_welcome_reaches_the_server_decoded()
    {
        using var harness = new Harness();
        harness.ConnectAndWelcome(networkId: 4u, teamId: 1);

        var frame = new ClientInputFrame(
            Tick: 77,
            NetworkId: 4u,
            Throttle: 1f,
            Steering: -0.5f,
            TurretYawRadians: 0.25f,
            Flags: InputFlags.Fire);
        harness.Client.SendInput(frame).Should().BeTrue();
        harness.Server.Pump();

        harness.ServerInputs.Should().ContainSingle();
        harness.ServerInputs[0].Peer.Should().Be(harness.Link.ClientPeerId);
        harness.ServerInputs[0].Frame.Should().Be(frame);
    }

    [Fact]
    public void Server_side_disconnect_flips_the_client_to_disconnected()
    {
        using var harness = new Harness();
        harness.ConnectAndWelcome(networkId: 1u, teamId: 0);

        harness.Link.Server.Disconnect(harness.Link.ClientPeerId);
        harness.Client.Pump();

        harness.Client.State.Should().Be(NetworkMatchConnectionState.Disconnected);
    }

    [Fact]
    public void Send_input_after_disconnect_returns_false()
    {
        using var harness = new Harness();
        harness.ConnectAndWelcome(networkId: 1u, teamId: 0);

        harness.Link.Server.Disconnect(harness.Link.ClientPeerId);
        harness.Client.Pump();

        harness.Client.SendInput(NeutralFrame).Should().BeFalse();
    }

    [Fact]
    public void Dispose_is_idempotent_and_pump_after_dispose_is_a_no_op()
    {
        var harness = new Harness();

        harness.Client.Dispose();
        var afterDispose = () =>
        {
            harness.Client.Dispose();
            harness.Client.Pump();
        };

        afterDispose.Should().NotThrow();
        harness.Dispose();
    }

    [Fact]
    public void An_owned_transport_is_closed_when_the_client_is_disposed()
    {
        var link = LoopbackTransportPair.Create();
        var client = new NetworkMatchClient(link.Client, ownsTransport: true);

        client.Dispose();

        link.Client.IsOpen.Should().BeFalse();
        link.Server.Dispose();
    }

    [Fact]
    public void A_borrowed_transport_outlives_client_disposal()
    {
        var link = LoopbackTransportPair.Create();
        var client = new NetworkMatchClient(link.Client, ownsTransport: false);

        client.Dispose();

        link.Client.IsOpen.Should().BeTrue();
        link.Client.Dispose();
        link.Server.Dispose();
    }

    [Fact]
    public void Connect_with_no_welcome_eventually_times_out_to_Failed()
    {
        var elapsed = TimeSpan.Zero;
        using var harness = new Harness(() => elapsed);

        harness.Client.Pump();
        harness.Client.State.Should().Be(
            NetworkMatchConnectionState.Connecting, "no welcome has arrived inside the deadline");

        elapsed = TimeSpan.FromSeconds(30);
        harness.Client.Pump();

        harness.Client.State.Should().Be(
            NetworkMatchConnectionState.Failed, "the connect deadline elapsed with no welcome");
    }

    [Fact]
    public void A_welcomed_client_never_times_out_the_connect()
    {
        var elapsed = TimeSpan.Zero;
        using var harness = new Harness(() => elapsed);
        harness.ConnectAndWelcome(networkId: 1u, teamId: 0);
        harness.Client.State.Should().Be(NetworkMatchConnectionState.Connected);

        // Long past the connect deadline — but the client is already connected, so the
        // connect timeout must not retroactively fail an established session.
        elapsed = TimeSpan.FromSeconds(30);
        harness.Client.Pump();

        harness.Client.State.Should().Be(NetworkMatchConnectionState.Connected);
    }

    private static WorldSnapshot SnapshotAtTick(long tick) =>
        new(
            new Tick(tick),
            new[]
            {
                new EntitySnapshot(
                    Id: 1,
                    Position: new Vector2(1f, 2f),
                    YawRadians: 0f,
                    TurretYawRadians: 0f,
                    StateFlags: EntityStateFlags.None),
            },
            Array.Empty<ProjectileSnapshot>());

    private sealed class Harness : IDisposable
    {
        public Harness(Func<TimeSpan>? elapsedSource = null)
        {
            Link = LoopbackTransportPair.Create();
            Client = new NetworkMatchClient(Link.Client, ownsTransport: false, elapsedSource: elapsedSource);
            Server = new ServerSession(Link.Server);
            Server.ClientInputReceived += (peer, frame) => ServerInputs.Add((peer, frame));
        }

        public LoopbackTransportLink Link { get; }

        public NetworkMatchClient Client { get; }

        public ServerSession Server { get; }

        public List<(ConnectionId Peer, ClientInputFrame Frame)> ServerInputs { get; } = new();

        public void PumpBoth()
        {
            Client.Pump();
            Server.Pump();
        }

        /// <summary>Drives the full handshake: surfaces Connected on both sides, then has
        /// the server send a Welcome the next client pump turns into the Connected state.</summary>
        public void ConnectAndWelcome(uint networkId, byte teamId)
        {
            PumpBoth();
            Server.SendWelcome(
                Link.ClientPeerId,
                new WelcomeFrame(networkId, teamId, WelcomeMatchModeKind.FreeForAll, RespawnsConfigured: 0, IsCommander: false))
                .Should().BeTrue();
            Client.Pump();
        }

        public void Dispose()
        {
            Client.Dispose();
            Server.Dispose();
            Link.Client.Dispose();
            Link.Server.Dispose();
        }
    }
}
