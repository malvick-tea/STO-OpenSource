using System;
using System.Buffers.Binary;
using FluentAssertions;
using Garupan.Sim.Protocol;
using Xunit;

namespace Garupan.Sim.Tests.Protocol;

/// <summary>Wire-level coverage for <see cref="MatchOverCodec"/> — the server → client
/// match-end frame. Same fixed-size shape as <see cref="WelcomeCodec"/>; the extra case
/// over <see cref="WelcomeCodecTests"/> is the out-of-range result discriminant, since
/// the result rides the wire as a raw <see cref="uint"/>.</summary>
public sealed class MatchOverCodecTests
{
    [Fact]
    public void Round_trip_preserves_a_free_for_all_winner()
    {
        var original = new MatchOverFrame(MatchOverResult.Winner, WinnerNetworkId: 42u, WinnerTeam: 3);
        var buffer = new byte[MatchOverWire.FrameBytes];

        MatchOverCodec.Encode(original, buffer).Should().Be(MatchOverWire.FrameBytes);

        var result = MatchOverCodec.TryDecode(buffer, out var decoded);
        result.Ok.Should().BeTrue();
        decoded.Should().Be(original);
    }

    [Fact]
    public void Round_trip_preserves_a_draw()
    {
        var original = new MatchOverFrame(MatchOverResult.Draw, WinnerNetworkId: 0u, WinnerTeam: 0);
        var buffer = new byte[MatchOverWire.FrameBytes];

        MatchOverCodec.Encode(original, buffer);

        var result = MatchOverCodec.TryDecode(buffer, out var decoded);
        result.Ok.Should().BeTrue();
        decoded.Should().Be(original);
    }

    [Fact]
    public void Magic_prefix_is_svoo_ascii()
    {
        var buffer = new byte[MatchOverWire.FrameBytes];
        MatchOverCodec.Encode(new MatchOverFrame(MatchOverResult.Winner, 1u, 1), buffer);

        buffer[0].Should().Be((byte)'S');
        buffer[1].Should().Be((byte)'V');
        buffer[2].Should().Be((byte)'O');
        buffer[3].Should().Be((byte)'O');
    }

    [Fact]
    public void Decode_rejects_truncated_buffer()
    {
        var bytes = new byte[MatchOverWire.FrameBytes - 1];

        var result = MatchOverCodec.TryDecode(bytes, out var decoded);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("shorter");
        decoded.Should().Be(default(MatchOverFrame));
    }

    [Fact]
    public void Decode_rejects_bad_magic()
    {
        var buffer = new byte[MatchOverWire.FrameBytes];
        MatchOverCodec.Encode(new MatchOverFrame(MatchOverResult.Winner, 1u, 1), buffer);
        buffer[0] = (byte)'X';

        var result = MatchOverCodec.TryDecode(buffer, out _);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("magic");
    }

    [Fact]
    public void Decode_rejects_wrong_version()
    {
        var buffer = new byte[MatchOverWire.FrameBytes];
        MatchOverCodec.Encode(new MatchOverFrame(MatchOverResult.Winner, 1u, 1), buffer);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4), 999u);

        var result = MatchOverCodec.TryDecode(buffer, out _);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("version");
    }

    [Fact]
    public void Decode_rejects_an_out_of_range_result_discriminant()
    {
        var buffer = new byte[MatchOverWire.FrameBytes];
        MatchOverCodec.Encode(new MatchOverFrame(MatchOverResult.Winner, 1u, 1), buffer);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(MatchOverWire.HeaderBytes), 2u);

        var result = MatchOverCodec.TryDecode(buffer, out _);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("result");
    }

    [Fact]
    public void Encode_throws_on_undersized_buffer()
    {
        var tooSmall = new byte[MatchOverWire.FrameBytes - 1];
        var act = () => MatchOverCodec.Encode(new MatchOverFrame(MatchOverResult.Winner, 1u, 1), tooSmall);
        act.Should().Throw<ArgumentException>().WithMessage("*too small*");
    }
}
