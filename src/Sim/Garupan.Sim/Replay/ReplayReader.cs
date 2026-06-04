using System;
using System.Buffers.Binary;
using System.Collections.Generic;
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
        out ReplayHeader header,
        out IReadOnlyList<ReplayFrame> frames)
    {
        header = default;
        frames = Array.Empty<ReplayFrame>();

        if (bytes.Length < ReplayWire.HeaderBytes)
        {
            return WireCodecResult.Fail("replay: truncated header");
        }

        if (!bytes[..4].SequenceEqual(ReplayWire.Magic))
        {
            return WireCodecResult.Fail("replay: bad magic");
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        if (version != ReplayWire.Version)
        {
            return WireCodecResult.Fail("replay: version mismatch");
        }

        var tickRateHz = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]);
        var startTickValue = (long)BinaryPrimitives.ReadUInt64LittleEndian(bytes[12..]);
        var frameCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[20..]);

        var output = new List<ReplayFrame>((int)frameCount);
        var cursor = ReplayWire.HeaderBytes;

        for (var i = 0u; i < frameCount; i++)
        {
            if (bytes.Length < cursor + ReplayWire.FrameHeaderBytes)
            {
                return WireCodecResult.Fail($"replay: truncated frame header at index {i}");
            }

            var tickOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[cursor..]);
            var snapLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(cursor + 4)..]);
            cursor += ReplayWire.FrameHeaderBytes;

            if (bytes.Length < cursor + snapLength)
            {
                return WireCodecResult.Fail($"replay: declared snapshot length exceeds buffer at frame {i}");
            }

            var snapResult = SnapshotDecoder.TryDecode(bytes.Slice(cursor, snapLength), out var snap);
            if (!snapResult.Ok)
            {
                return WireCodecResult.Fail($"replay: snapshot at frame {i} did not decode: {snapResult.Error}");
            }

            cursor += snapLength;
            output.Add(new ReplayFrame(new Tick(startTickValue + tickOffset), snap));
        }

        header = new ReplayHeader(version, tickRateHz, new Tick(startTickValue), output.Count);
        frames = output;
        return WireCodecResult.Success;
    }
}
