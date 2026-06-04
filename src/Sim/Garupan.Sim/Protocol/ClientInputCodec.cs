using System;
using System.Buffers.Binary;
using Garupan.Sim.Components;

namespace Garupan.Sim.Protocol;

/// <summary>
/// Encode / decode for <see cref="ClientInputFrame"/>. Fixed-size; encode never fails on
/// a sized buffer, decode rejects truncated input, bad magic, and version mismatch.
///
/// Ported byte-for-byte from <c>svo::protocol::client_input_codec.cpp</c>; a frame
/// encoded here decodes on the C++ side and vice versa, which keeps the future
/// authoritative C# server / C++ client (or the inverse) compatible.
/// </summary>
public static class ClientInputCodec
{
    public static int Encode(ClientInputFrame frame, Span<byte> buffer)
    {
        if (buffer.Length < ClientInputWire.FrameBytes)
        {
            throw new ArgumentException(
                $"client input encode buffer too small: need {ClientInputWire.FrameBytes}, got {buffer.Length}",
                nameof(buffer));
        }

        ClientInputWire.Magic.AsSpan().CopyTo(buffer);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[4..], ProtocolVersion.Wire);

        var body = buffer[ClientInputWire.HeaderBytes..];
        BinaryPrimitives.WriteUInt64LittleEndian(body[0..], frame.Tick);
        BinaryPrimitives.WriteUInt32LittleEndian(body[8..], frame.NetworkId);
        BinaryPrimitives.WriteSingleLittleEndian(body[12..], frame.Throttle);
        BinaryPrimitives.WriteSingleLittleEndian(body[16..], frame.Steering);
        BinaryPrimitives.WriteSingleLittleEndian(body[20..], frame.TurretYawRadians);
        BinaryPrimitives.WriteUInt32LittleEndian(body[24..], (uint)frame.Flags);
        BinaryPrimitives.WriteSingleLittleEndian(body[28..], frame.BarrelPitchRadians);

        return ClientInputWire.FrameBytes;
    }

    public static WireCodecResult TryDecode(ReadOnlySpan<byte> bytes, out ClientInputFrame frame)
    {
        frame = default;

        if (bytes.Length < ClientInputWire.FrameBytes)
        {
            return WireCodecResult.Fail("client input frame: buffer shorter than fixed size");
        }

        if (!bytes[..4].SequenceEqual(ClientInputWire.Magic))
        {
            return WireCodecResult.Fail("client input frame: bad magic");
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        if (version != ProtocolVersion.Wire)
        {
            return WireCodecResult.Fail("client input frame: protocol version mismatch");
        }

        var body = bytes[ClientInputWire.HeaderBytes..];
        frame = new ClientInputFrame(
            Tick: BinaryPrimitives.ReadUInt64LittleEndian(body[0..]),
            NetworkId: BinaryPrimitives.ReadUInt32LittleEndian(body[8..]),
            Throttle: BinaryPrimitives.ReadSingleLittleEndian(body[12..]),
            Steering: BinaryPrimitives.ReadSingleLittleEndian(body[16..]),
            TurretYawRadians: BinaryPrimitives.ReadSingleLittleEndian(body[20..]),
            Flags: (InputFlags)BinaryPrimitives.ReadUInt32LittleEndian(body[24..]),
            BarrelPitchRadians: BinaryPrimitives.ReadSingleLittleEndian(body[28..]));

        return WireCodecResult.Success;
    }
}
