using System;
using System.Buffers.Binary;
using FluentAssertions;
using Garupan.Sim.Protocol;
using Xunit;

namespace Garupan.Sim.Tests.Protocol;

public sealed class WelcomeCodecTests
{
    [Fact]
    public void Round_trip_preserves_every_field()
    {
        var original = new WelcomeFrame(
            NetworkId: 42, TeamId: 7,
            ModeKind: WelcomeMatchModeKind.TeamTactical, RespawnsConfigured: 1, IsCommander: true);
        var buffer = new byte[WelcomeWire.FrameBytes];

        WelcomeCodec.Encode(original, buffer).Should().Be(WelcomeWire.FrameBytes);

        var result = WelcomeCodec.TryDecode(buffer, out var decoded);
        result.Ok.Should().BeTrue();
        decoded.Should().Be(original);
    }

    [Fact]
    public void Round_trip_carries_free_for_all_mode_and_three_respawns()
    {
        var original = new WelcomeFrame(
            NetworkId: 9, TeamId: 1,
            ModeKind: WelcomeMatchModeKind.FreeForAll, RespawnsConfigured: 3, IsCommander: false);
        var buffer = new byte[WelcomeWire.FrameBytes];

        WelcomeCodec.Encode(original, buffer);

        WelcomeCodec.TryDecode(buffer, out var decoded);
        decoded.ModeKind.Should().Be(WelcomeMatchModeKind.FreeForAll);
        decoded.RespawnsConfigured.Should().Be((byte)3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Round_trip_carries_the_commander_flag(bool isCommander)
    {
        var original = new WelcomeFrame(
            NetworkId: 3, TeamId: 0,
            ModeKind: WelcomeMatchModeKind.TeamTactical, RespawnsConfigured: 1, IsCommander: isCommander);
        var buffer = new byte[WelcomeWire.FrameBytes];

        WelcomeCodec.Encode(original, buffer);

        WelcomeCodec.TryDecode(buffer, out var decoded);
        decoded.IsCommander.Should().Be(isCommander);
    }

    [Fact]
    public void Magic_prefix_is_svow_ascii()
    {
        var frame = new WelcomeFrame(
            NetworkId: 1, TeamId: 1, WelcomeMatchModeKind.FreeForAll, RespawnsConfigured: 0, IsCommander: false);
        var buffer = new byte[WelcomeWire.FrameBytes];
        WelcomeCodec.Encode(frame, buffer);

        buffer[0].Should().Be((byte)'S');
        buffer[1].Should().Be((byte)'V');
        buffer[2].Should().Be((byte)'O');
        buffer[3].Should().Be((byte)'W');
    }

    [Fact]
    public void Decode_rejects_unknown_mode_kind()
    {
        var buffer = new byte[WelcomeWire.FrameBytes];
        WelcomeCodec.Encode(
            new WelcomeFrame(1, 0, WelcomeMatchModeKind.FreeForAll, 0, false),
            buffer);
        buffer[WelcomeWire.HeaderBytes + 5] = 0xFF;

        var result = WelcomeCodec.TryDecode(buffer, out _);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("mode");
    }

    [Fact]
    public void Decode_rejects_truncated_buffer()
    {
        var bytes = new byte[WelcomeWire.FrameBytes - 1];

        var result = WelcomeCodec.TryDecode(bytes, out var decoded);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("shorter");
        decoded.Should().Be(default(WelcomeFrame));
    }

    [Fact]
    public void Decode_rejects_bad_magic()
    {
        var buffer = new byte[WelcomeWire.FrameBytes];
        buffer[0] = (byte)'X';

        var result = WelcomeCodec.TryDecode(buffer, out _);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("magic");
    }

    [Fact]
    public void Decode_rejects_wrong_version()
    {
        var buffer = new byte[WelcomeWire.FrameBytes];
        WelcomeCodec.Encode(new WelcomeFrame(1, 1, WelcomeMatchModeKind.FreeForAll, 0, false), buffer);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4), 999u);

        var result = WelcomeCodec.TryDecode(buffer, out _);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("version");
    }

    [Fact]
    public void Encode_throws_on_undersized_buffer()
    {
        var tooSmall = new byte[WelcomeWire.FrameBytes - 1];
        var act = () => WelcomeCodec.Encode(
            new WelcomeFrame(1, 1, WelcomeMatchModeKind.FreeForAll, 0, false), tooSmall);
        act.Should().Throw<ArgumentException>().WithMessage("*too small*");
    }
}
