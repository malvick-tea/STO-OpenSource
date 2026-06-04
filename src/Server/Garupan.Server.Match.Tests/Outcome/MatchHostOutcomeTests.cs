using System;
using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using FluentAssertions;
using Garupan.Content;
using Garupan.Net.Session;
using Garupan.Server.Match.Outcome;
using Garupan.Sim.Components;
using Garupan.Sim.Protocol;
using Opus.Net.Loopback;
using Xunit;

namespace Garupan.Server.Match.Tests.Outcome;

/// <summary>
/// Integration coverage for <see cref="MatchHost"/>'s outcome wiring — peers connect
/// through a real <see cref="LoopbackTransportHub"/>, a tank is knocked out directly in
/// the authoritative world, and the test asserts the host's
/// <see cref="MatchHost.Outcome"/> latches and the match freezes. The pure rule
/// arithmetic is covered headlessly by <see cref="MatchOutcomeTrackerTests"/>; these
/// tests pin the host's per-tick roster build + freeze behaviour.
/// </summary>
public sealed class MatchHostOutcomeTests
{
    [Fact]
    public void A_fresh_host_reports_an_in_progress_outcome()
    {
        using var harness = new OutcomeHarness();

        harness.Host.Outcome.IsDecided.Should().BeFalse();
        harness.Host.Outcome.Kind.Should().Be(MatchOutcomeKind.InProgress);
    }

    [Fact]
    public void A_two_peer_match_with_both_tanks_healthy_stays_in_progress()
    {
        using var harness = new OutcomeHarness();

        harness.PumpTicks(10);

        harness.Host.Outcome.Kind.Should().Be(MatchOutcomeKind.InProgress);
    }

    [Fact]
    public void A_lone_peer_match_never_decides()
    {
        using var harness = new OutcomeHarness(peerCount: 1);

        harness.PumpTicks(10);

        harness.Host.Outcome.IsDecided.Should().BeFalse();
    }

    [Fact]
    public void Knocking_out_one_tank_decides_the_free_for_all_for_the_survivor()
    {
        using var harness = new OutcomeHarness();
        harness.PumpOnce();
        harness.Welcomes.Should().HaveCount(2);
        var survivorNetworkId = harness.Welcomes[1].NetworkId;

        KnockOutTank(harness.Host, harness.Welcomes[0].NetworkId);
        harness.PumpOnce();

        harness.Host.Outcome.Kind.Should().Be(MatchOutcomeKind.Winner);
        harness.Host.Outcome.WinnerNetworkId.Should().Be(survivorNetworkId);
    }

    [Fact]
    public void A_decided_match_broadcasts_a_match_over_frame_to_every_peer()
    {
        using var harness = new OutcomeHarness();
        harness.PumpOnce();
        harness.Welcomes.Should().HaveCount(2);
        var survivorNetworkId = harness.Welcomes[1].NetworkId;

        KnockOutTank(harness.Host, harness.Welcomes[0].NetworkId);
        harness.PumpOnce();

        harness.MatchOvers.Should().HaveCount(2, "the verdict fans out to every connected peer");
        harness.MatchOvers.Should().OnlyContain(
            f => f.Result == MatchOverResult.Winner && f.WinnerNetworkId == survivorNetworkId);
    }

    [Fact]
    public void A_decided_match_freezes_the_snapshot_stream()
    {
        using var harness = new OutcomeHarness();
        harness.PumpOnce();
        KnockOutTank(harness.Host, harness.Welcomes[0].NetworkId);
        harness.PumpOnce();
        harness.Host.Outcome.IsDecided.Should().BeTrue();

        var broadcastsAtDecision = harness.Host.SnapshotsBroadcast;
        harness.PumpTicks(5);

        harness.Host.SnapshotsBroadcast.Should().Be(
            broadcastsAtDecision,
            "a decided match stops broadcasting — the sim is frozen");
    }

    [Fact]
    public void A_knock_out_with_respawn_budget_does_not_decide_the_match()
    {
        using var harness = new OutcomeHarness(respawns: 3, respawnDelayTicks: 30);
        harness.PumpOnce();
        harness.Welcomes.Should().HaveCount(2);

        KnockOutTank(harness.Host, harness.Welcomes[0].NetworkId);
        harness.PumpOnce();

        harness.Host.Outcome.IsDecided.Should().BeFalse(
            "the knocked-out peer has respawn budget — the match is not over");
    }

    [Fact]
    public void A_knock_out_exhausting_the_last_life_decides_the_match()
    {
        using var harness = new OutcomeHarness(respawns: 1, respawnDelayTicks: 1);
        harness.PumpOnce();
        harness.Welcomes.Should().HaveCount(2);
        var survivorNetworkId = harness.Welcomes[1].NetworkId;

        // First knock-out — consumes the respawn, queues a timer.
        KnockOutTank(harness.Host, harness.Welcomes[0].NetworkId);
        harness.PumpOnce();
        harness.Host.Outcome.IsDecided.Should().BeFalse();

        // Wait out the respawn delay so the tank comes back, then knock it out a second
        // time — now with zero respawn budget. The next tick decides the match.
        harness.PumpTicks(2);
        KnockOutTank(harness.Host, harness.Welcomes[0].NetworkId);
        harness.PumpOnce();

        harness.Host.Outcome.Kind.Should().Be(MatchOutcomeKind.Winner);
        harness.Host.Outcome.WinnerNetworkId.Should().Be(survivorNetworkId);
    }

    [Fact]
    public void A_decided_match_with_auto_reset_recycles_after_the_hold_window()
    {
        using var harness = new OutcomeHarness(postMatchHoldTicks: 2);
        harness.PumpOnce();
        harness.Welcomes.Should().HaveCount(2);
        var initialWelcomeIds = new[] { harness.Welcomes[0].NetworkId, harness.Welcomes[1].NetworkId };

        KnockOutTank(harness.Host, initialWelcomeIds[0]);
        harness.PumpOnce(); // deciding tick — verdict broadcast, hold counter armed at 2.
        harness.Host.Outcome.IsDecided.Should().BeTrue();
        harness.Host.MatchesPlayed.Should().Be(0, "the first match is not yet recorded as played");

        // Two more ticks elapse the hold counter (2 → 1 → 0); the third triggers reset.
        harness.PumpTicks(3);

        harness.Host.Outcome.IsDecided.Should().BeFalse("the tracker was reset");
        harness.Host.MatchesPlayed.Should().Be(1, "one match has finished and a new one started");
        harness.Welcomes.Should().HaveCount(4, "each peer received a fresh Welcome on reset");
        initialWelcomeIds.Should().NotContain(harness.Welcomes[2].NetworkId, "the new match draws fresh NetworkIds");
        initialWelcomeIds.Should().NotContain(harness.Welcomes[3].NetworkId);
    }

    [Fact]
    public void Reset_with_hold_zero_freezes_indefinitely_until_called_manually()
    {
        using var harness = new OutcomeHarness(postMatchHoldTicks: 0);
        harness.PumpOnce();
        KnockOutTank(harness.Host, harness.Welcomes[0].NetworkId);
        harness.PumpOnce();
        harness.Host.Outcome.IsDecided.Should().BeTrue();

        harness.PumpTicks(20);

        harness.Host.MatchesPlayed.Should().Be(0, "auto-reset is disabled; the match stays frozen");
        harness.Host.Outcome.IsDecided.Should().BeTrue();

        harness.Host.ResetMatch();
        harness.PumpOnce();

        harness.Host.MatchesPlayed.Should().Be(1);
        harness.Host.Outcome.IsDecided.Should().BeFalse();
    }

    [Fact]
    public void Reset_clears_match_over_state_on_each_connected_client()
    {
        using var harness = new OutcomeHarness(postMatchHoldTicks: 1);
        harness.PumpOnce();
        KnockOutTank(harness.Host, harness.Welcomes[0].NetworkId);
        harness.PumpOnce(); // verdict broadcast.
        harness.MatchOvers.Should().HaveCount(2, "verdict reaches both peers");

        // Hold counter (1 → 0); the next tick triggers reset + sends fresh Welcomes.
        harness.PumpTicks(2);

        harness.Welcomes.Should().HaveCount(4, "each peer got a fresh Welcome on reset");
        harness.Host.Outcome.IsDecided.Should().BeFalse();
    }

    /// <summary>Knocks out the tank carrying <paramref name="networkId"/> by stamping the
    /// <see cref="KnockedOut"/> tag straight onto the authoritative world — the test's
    /// stand-in for a real penetrating hit, which would need the full combat sim.</summary>
    private static void KnockOutTank(MatchHost host, uint networkId)
    {
        var raw = host.World.Raw;
        var query = new QueryDescription().WithAll<NetworkId>();
        Entity target = default;
        var found = false;
        raw.Query(in query, (Entity entity, ref NetworkId id) =>
        {
            if (id.Value == networkId)
            {
                target = entity;
                found = true;
            }
        });

        found.Should().BeTrue($"network id {networkId} must map to a live tank in the host world");
        raw.Add(target, default(KnockedOut));
    }

    private sealed class OutcomeHarness : IDisposable
    {
        private const double OneTickDelta = 1.0 / 60.0;

        public OutcomeHarness(
            int peerCount = 2,
            MatchOutcomeRule rule = MatchOutcomeRule.LastTankStanding,
            byte respawns = 0,
            ushort respawnDelayTicks = 60,
            int postMatchHoldTicks = 0)
        {
            Hub = LoopbackTransportHub.Create();
            Host = new MatchHost(Hub, new MatchHostOptions(
                PlayerSpec: TankRoster.VehicleMediumA,
                PlayerTeam: Team.PlayerSchool,
                SpawnAnchor: Vector2.Zero,
                OutcomeRule: rule,
                RespawnsPerPeer: respawns,
                RespawnDelayTicks: respawnDelayTicks,
                PostMatchHoldTicks: postMatchHoldTicks));

            Clients = new ClientSession[peerCount];
            for (var i = 0; i < peerCount; i++)
            {
                var connection = Hub.Accept($"outcome-client-{i}");
                var session = new ClientSession(connection.Client);
                session.WelcomeReceived += w => Welcomes.Add(w);
                session.MatchOverReceived += m => MatchOvers.Add(m);
                Clients[i] = session;
            }
        }

        public LoopbackTransportHub Hub { get; }

        public MatchHost Host { get; }

        public ClientSession[] Clients { get; }

        public List<WelcomeFrame> Welcomes { get; } = new();

        public List<MatchOverFrame> MatchOvers { get; } = new();

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
