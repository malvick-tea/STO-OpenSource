using System;
using System.Buffers.Binary;

namespace Garupan.Sim.Protocol;

/// <summary>
/// Encode / decode for <see cref="MatchOverFrame"/>. Fixed-size; encode never fails on a
/// sized buffer; decode rejects truncated input, bad magic, version mismatch, and an
/// out-of-range result discriminant.
///
/// Same shape as <see cref="WelcomeCodec"/> — a server → client fixed-size frame.
/// </summary>
public static class MatchOverCodec
{
    public static int Encode(MatchOverFrame frame, Span<byte> buffer)
    {
        if (buffer.Length < MatchOverWire.FrameBytes)
        {
            throw new ArgumentException(
                $"match-over encode buffer too small: need {MatchOverWire.FrameBytes}, got {buffer.Length}",
                nameof(buffer));
        }

        MatchOverWire.Magic.AsSpan().CopyTo(buffer);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[4..], ProtocolVersion.Wire);

        var body = buffer[MatchOverWire.HeaderBytes..];
        BinaryPrimitives.WriteUInt32LittleEndian(body[0..], (uint)frame.Result);
        BinaryPrimitives.WriteUInt32LittleEndian(body[4..], frame.WinnerNetworkId);
        body[8] = frame.WinnerTeam;

        return MatchOverWire.FrameBytes;
    }

    public static WireCodecResult TryDecode(ReadOnlySpan<byte> bytes, out MatchOverFrame frame)
    {
        frame = default;

        if (bytes.Length < MatchOverWire.FrameBytes)
        {
            return WireCodecResult.Fail("match-over frame: buffer shorter than fixed size");
        }

        if (!bytes[..4].SequenceEqual(MatchOverWire.Magic))
        {
            return WireCodecResult.Fail("match-over frame: bad magic");
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        if (version != ProtocolVersion.Wire)
        {
            return WireCodecResult.Fail("match-over frame: protocol version mismatch");
        }

        var body = bytes[MatchOverWire.HeaderBytes..];
        var result = BinaryPrimitives.ReadUInt32LittleEndian(body[0..]);
        if (result > (uint)MatchOverResult.Draw)
        {
            return WireCodecResult.Fail("match-over frame: result discriminant out of range");
        }

        frame = new MatchOverFrame(
            (MatchOverResult)result,
            BinaryPrimitives.ReadUInt32LittleEndian(body[4..]),
            body[8]);

        return WireCodecResult.Success;
    }
}
