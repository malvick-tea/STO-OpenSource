using System;
using FluentAssertions;
using Garupan.Sim.Protocol;
using Garupan.Sim.Snapshot;
using Xunit;

namespace Garupan.Net.Session.Tests;

/// <summary>Pure-CPU coverage for <see cref="NetMessageDispatcher.Classify"/>. The
/// classifier is on every receive event's hot path; these cases pin the magic-prefix →
/// <see cref="NetMessageKind"/> mapping so a renamed wire layout or a re-ordered enum
/// fails loudly at test time instead of in runtime silently dispatching the wrong
/// codec.</summary>
public sealed class NetMessageDispatcherTests
{
    [Fact]
    public void Classify_returns_unknown_for_payload_shorter_than_magic_prefix()
    {
        var stub = new byte[] { (byte)'S', (byte)'V', (byte)'O' };

        NetMessageDispatcher.Classify(stub).Should().Be(NetMessageKind.Unknown);
    }

    [Fact]
    public void Classify_returns_unknown_for_empty_payload()
    {
        NetMessageDispatcher.Classify(ReadOnlySpan<byte>.Empty).Should().Be(NetMessageKind.Unknown);
    }

    [Fact]
    public void Classify_routes_svow_to_welcome()
    {
        var welcome = EncodeFrame(
            static buffer => WelcomeCodec.Encode(
            new WelcomeFrame(NetworkId: 1u, TeamId: 0, WelcomeMatchModeKind.FreeForAll, RespawnsConfigured: 0, IsCommander: false),
            buffer), WelcomeWire.FrameBytes);

        NetMessageDispatcher.Classify(welcome).Should().Be(NetMessageKind.Welcome);
    }

    [Fact]
    public void Classify_routes_svoi_to_clientinput()
    {
        var input = EncodeFrame(
            static buffer => ClientInputCodec.Encode(
                new ClientInputFrame(
                    Tick: 0,
                    NetworkId: 0,
                    Throttle: 0,
                    Steering: 0,
                    TurretYawRadians: 0,
                    Flags: 0),
                buffer),
            ClientInputWire.FrameBytes);

        NetMessageDispatcher.Classify(input).Should().Be(NetMessageKind.ClientInput);
    }

    [Fact]
    public void Classify_routes_svos_to_snapshot()
    {
        var snap = new WorldSnapshot(
            Opus.Foundation.Tick.Zero,
            Array.Empty<EntitySnapshot>(),
            Array.Empty<ProjectileSnapshot>());
        var buffer = new byte[SnapshotWire.EncodedSize(snap)];
        SnapshotEncoder.Encode(snap, buffer);

        NetMessageDispatcher.Classify(buffer).Should().Be(NetMessageKind.Snapshot);
    }

    [Fact]
    public void Classify_routes_svoo_to_matchover()
    {
        var matchOver = EncodeFrame(
            static buffer => MatchOverCodec.Encode(
                new MatchOverFrame(MatchOverResult.Winner, WinnerNetworkId: 1u, WinnerTeam: 0),
                buffer),
            MatchOverWire.FrameBytes);

        NetMessageDispatcher.Classify(matchOver).Should().Be(NetMessageKind.MatchOver);
    }

    [Fact]
    public void Classify_returns_unknown_for_unrecognised_magic()
    {
        var unknown = new byte[] { (byte)'X', (byte)'X', (byte)'X', (byte)'X', 0 };

        NetMessageDispatcher.Classify(unknown).Should().Be(NetMessageKind.Unknown);
    }

    private static byte[] EncodeFrame(EncodeAction encode, int sizeBytes)
    {
        var buffer = new byte[sizeBytes];
        encode(buffer);
        return buffer;
    }

    private delegate void EncodeAction(Span<byte> buffer);
}
