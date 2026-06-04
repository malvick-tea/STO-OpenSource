using System;
using System.Buffers.Binary;
using FluentAssertions;
using Garupan.Sim.Components;
using Garupan.Sim.Protocol;
using Xunit;

namespace Garupan.Sim.Tests.Protocol;

public sealed class ClientInputCodecTests
{
    [Fact]
    public void Round_trip_preserves_every_field()
    {
        var original = new ClientInputFrame(
            Tick: 1234567890UL,
            NetworkId: 42,
            Throttle: 0.5f,
            Steering: -0.75f,
            TurretYawRadians: 1.25f,
            Flags: InputFlags.Fire,
            BarrelPitchRadians: 0.2f);
        var buffer = new byte[ClientInputWire.FrameBytes];

        ClientInputCodec.Encode(original, buffer).Should().Be(ClientInputWire.FrameBytes);

        var result = ClientInputCodec.TryDecode(buffer, out var decoded);
        result.Ok.Should().BeTrue();
        decoded.Should().Be(original);
    }

    [Fact]
    public void Magic_prefix_is_svoi_ascii()
    {
        var frame = new ClientInputFrame(0, 0, 0f, 0f, 0f, InputFlags.None);
        var buffer = new byte[ClientInputWire.FrameBytes];
        ClientInputCodec.Encode(frame, buffer);

        buffer[0].Should().Be((byte)'S');
        buffer[1].Should().Be((byte)'V');
        buffer[2].Should().Be((byte)'O');
        buffer[3].Should().Be((byte)'I');
    }

    [Fact]
    public void Decode_rejects_truncated_buffer()
    {
        var bytes = new byte[ClientInputWire.FrameBytes - 1];

        var result = ClientInputCodec.TryDecode(bytes, out var decoded);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("shorter");
        decoded.Should().Be(default(ClientInputFrame));
    }

    [Fact]
    public void Decode_rejects_bad_magic()
    {
        var buffer = new byte[ClientInputWire.FrameBytes];
        buffer[0] = (byte)'X';

        var result = ClientInputCodec.TryDecode(buffer, out _);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("magic");
    }

    [Fact]
    public void Decode_rejects_wrong_version()
    {
        var buffer = new byte[ClientInputWire.FrameBytes];
        ClientInputCodec.Encode(new ClientInputFrame(1, 1, 0f, 0f, 0f, InputFlags.None), buffer);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4), 999u);

        var result = ClientInputCodec.TryDecode(buffer, out _);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("version");
    }

    [Fact]
    public void Flags_bitfield_round_trips_through_uint32()
    {
        var fire = new ClientInputFrame(0, 0, 0f, 0f, 0f, InputFlags.Fire);
        var none = new ClientInputFrame(0, 0, 0f, 0f, 0f, InputFlags.None);
        var buffer = new byte[ClientInputWire.FrameBytes];

        ClientInputCodec.Encode(fire, buffer);
        ClientInputCodec.TryDecode(buffer, out var decoded);
        decoded.Flags.Should().Be(InputFlags.Fire);

        ClientInputCodec.Encode(none, buffer);
        ClientInputCodec.TryDecode(buffer, out decoded);
        decoded.Flags.Should().Be(InputFlags.None);
    }

    [Fact]
    public void Encode_throws_on_undersized_buffer()
    {
        var tooSmall = new byte[ClientInputWire.FrameBytes - 1];
        var act = () => ClientInputCodec.Encode(new ClientInputFrame(0, 0, 0f, 0f, 0f, InputFlags.None), tooSmall);
        act.Should().Throw<ArgumentException>().WithMessage("*too small*");
    }
}
