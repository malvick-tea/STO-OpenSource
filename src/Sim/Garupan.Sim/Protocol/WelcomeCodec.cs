using System;
using System.Buffers.Binary;

namespace Garupan.Sim.Protocol;

/// <summary>
/// Encode / decode for <see cref="WelcomeFrame"/>. Fixed-size; encode never fails on a
/// sized buffer, decode rejects truncated input, bad magic, and version mismatch.
///
/// Ported byte-for-byte from <c>svo::protocol::welcome_codec.cpp</c>; a frame encoded
/// here decodes on the C++ side and vice versa.
/// </summary>
public static class WelcomeCodec
{
    public static int Encode(WelcomeFrame frame, Span<byte> buffer)
    {
        if (buffer.Length < WelcomeWire.FrameBytes)
        {
            throw new ArgumentException(
                $"welcome encode buffer too small: need {WelcomeWire.FrameBytes}, got {buffer.Length}",
                nameof(buffer));
        }

        WelcomeWire.Magic.AsSpan().CopyTo(buffer);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[4..], ProtocolVersion.Wire);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[WelcomeWire.HeaderBytes..], frame.NetworkId);
        buffer[WelcomeWire.HeaderBytes + 4] = frame.TeamId;
        buffer[WelcomeWire.HeaderBytes + 5] = (byte)frame.ModeKind;
        buffer[WelcomeWire.HeaderBytes + 6] = frame.RespawnsConfigured;
        buffer[WelcomeWire.HeaderBytes + 7] = frame.IsCommander ? (byte)1 : (byte)0;

        return WelcomeWire.FrameBytes;
    }

    public static WireCodecResult TryDecode(ReadOnlySpan<byte> bytes, out WelcomeFrame frame)
    {
        frame = default;

        if (bytes.Length < WelcomeWire.FrameBytes)
        {
            return WireCodecResult.Fail("welcome frame: buffer shorter than fixed size");
        }

        if (!bytes[..4].SequenceEqual(WelcomeWire.Magic))
        {
            return WireCodecResult.Fail("welcome frame: bad magic");
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        if (version != ProtocolVersion.Wire)
        {
            return WireCodecResult.Fail("welcome frame: protocol version mismatch");
        }

        var networkId = BinaryPrimitives.ReadUInt32LittleEndian(bytes[WelcomeWire.HeaderBytes..]);
        var teamId = bytes[WelcomeWire.HeaderBytes + 4];
        var modeKindByte = bytes[WelcomeWire.HeaderBytes + 5];
        if (modeKindByte > (byte)WelcomeMatchModeKind.TeamTactical)
        {
            return WireCodecResult.Fail($"welcome frame: unknown mode kind {modeKindByte}");
        }

        var respawnsConfigured = bytes[WelcomeWire.HeaderBytes + 6];
        var isCommander = bytes[WelcomeWire.HeaderBytes + 7] != 0;
        frame = new WelcomeFrame(
            networkId,
            teamId,
            (WelcomeMatchModeKind)modeKindByte,
            respawnsConfigured,
            isCommander);

        return WireCodecResult.Success;
    }
}
