using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using Garupan.Sim.Protocol;
using Garupan.Sim.Snapshot;
using Opus.Foundation;

namespace Garupan.Sim.Replay;

/// <summary>
/// Parses a byte sequence produced by <see cref="ReplayWriter.Build"/> back into a
/// <see cref="ReplayHeader"/> and a list of <see cref="ReplayFrame"/>s. Pure read,
/// stream-shape only — no IO. On failure both outputs hold safe empties and the result
/// carries a literal error message.
/// </summary>
public static class ReplayReader
{
    public static WireCodecResult TryRead(
        ReadOnlySpan<byte> bytes,
        ReadOnlySpan<byte> authenticationKey,
        out ReplayHeader header,
        out IReadOnlyList<ReplayFrame> frames)
    {
        header = default;
        frames = Array.Empty<ReplayFrame>();

        if (authenticationKey.Length < ReplayWire.MinimumAuthenticationKeyBytes)
        {
            return WireCodecResult.Fail("replay: authentication key is too short");
        }

        if (bytes.Length < ReplayWire.HeaderBytes + ReplayWire.AuthenticationTagBytes
            || bytes.Length > ReplayWire.MaxReplayBytes)
        {
            return WireCodecResult.Fail("replay: truncated header");
        }

        var payloadLength = bytes.Length - ReplayWire.AuthenticationTagBytes;
        var payload = bytes[..payloadLength];
        Span<byte> expectedTag = stackalloc byte[ReplayWire.AuthenticationTagBytes];
        HMACSHA256.HashData(authenticationKey, payload, expectedTag);
        if (!CryptographicOperations.FixedTimeEquals(
                expectedTag,
                bytes[payloadLength..]))
        {
            return WireCodecResult.Fail("replay: authentication failed");
        }

        if (!payload[..4].SequenceEqual(ReplayWire.Magic))
        {
            return WireCodecResult.Fail("replay: bad magic");
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(payload[4..]);
        if (version != ReplayWire.Version)
        {
            return WireCodecResult.Fail("replay: version mismatch");
        }

        var tickRateValue = BinaryPrimitives.ReadUInt32LittleEndian(payload[8..]);
        var startTickRaw = BinaryPrimitives.ReadUInt64LittleEndian(payload[12..]);
        var frameCount = BinaryPrimitives.ReadUInt32LittleEndian(payload[20..]);
        var maxFramesByBuffer = (payload.Length - ReplayWire.HeaderBytes) / ReplayWire.FrameHeaderBytes;
        if (tickRateValue is 0 or > ReplayWire.MaximumTickRateHz
            || startTickRaw > long.MaxValue
            || frameCount > ReplayWire.MaxReplayFrames
            || frameCount > (uint)maxFramesByBuffer)
        {
            return WireCodecResult.Fail("replay: header values exceed safety limits");
        }

        var tickRateHz = (int)tickRateValue;
        var startTickValue = (long)startTickRaw;
        var output = new List<ReplayFrame>(checked((int)frameCount));
        var cursor = ReplayWire.HeaderBytes;

        for (var i = 0u; i < frameCount; i++)
        {
            if (payload.Length < cursor + ReplayWire.FrameHeaderBytes)
            {
                return WireCodecResult.Fail($"replay: truncated frame header at index {i}");
            }

            var tickOffset = BinaryPrimitives.ReadUInt32LittleEndian(payload[cursor..]);
            var snapLengthRaw = BinaryPrimitives.ReadUInt32LittleEndian(payload[(cursor + 4)..]);
            cursor += ReplayWire.FrameHeaderBytes;

            if (snapLengthRaw > SnapshotWire.MaxEncodedBytes
                || (long)cursor + snapLengthRaw > payload.Length)
            {
                return WireCodecResult.Fail($"replay: declared snapshot length exceeds buffer at frame {i}");
            }

            var snapLength = checked((int)snapLengthRaw);
            var snapResult = SnapshotDecoder.TryDecode(payload.Slice(cursor, snapLength), out var snap);
            if (!snapResult.Ok)
            {
                return WireCodecResult.Fail($"replay: snapshot at frame {i} did not decode: {snapResult.Error}");
            }

            cursor += snapLength;
            if ((long)tickOffset > long.MaxValue - startTickValue)
            {
                return WireCodecResult.Fail($"replay: tick overflow at frame {i}");
            }

            output.Add(new ReplayFrame(new Tick(startTickValue + tickOffset), snap));
        }

        if (cursor != payload.Length)
        {
            return WireCodecResult.Fail("replay: trailing payload bytes");
        }

        header = new ReplayHeader(version, tickRateHz, new Tick(startTickValue), output.Count);
        frames = output;
        return WireCodecResult.Success;
    }
}
