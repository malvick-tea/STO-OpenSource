using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Garupan.Sim.Snapshot;
using Opus.Foundation;

namespace Garupan.Sim.Replay;

/// <summary>
/// Accumulates <see cref="WorldSnapshot"/> captures into an in-memory replay stream.
/// Each <see cref="RecordSnapshot"/> call encodes the snapshot once through
/// <see cref="SnapshotEncoder"/> and stashes the bytes; <see cref="Build"/> finalises
/// the header and emits the complete byte sequence.
///
/// Phase-0 writer is memory-backed because the recorder runs in the simulation tick
/// loop and we don't want disk latency in the hot path. A future
/// <c>ReplayStreamWriter</c> can swap the buffer for a <see cref="FileStream"/> when
/// hour-long replays grow past comfortable memory budgets.
/// </summary>
public sealed class ReplayWriter
{
    private readonly int _tickRateHz;
    private readonly Tick _startTick;
    private readonly List<(uint TickOffset, byte[] Bytes)> _frames = new();

    public ReplayWriter(int tickRateHz, Tick startTick)
    {
        if (tickRateHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickRateHz), tickRateHz, "Tick rate must be positive.");
        }

        _tickRateHz = tickRateHz;
        _startTick = startTick;
    }

    public int FrameCount => _frames.Count;

    public void RecordSnapshot(WorldSnapshot snap)
    {
        Ensure.NotNull(snap);

        var offset = snap.Tick.Value - _startTick.Value;
        if (offset < 0)
        {
            throw new ArgumentException(
                $"snapshot tick {snap.Tick} precedes recorder start tick {_startTick}; replays must be monotonic",
                nameof(snap));
        }

        if (offset > uint.MaxValue)
        {
            throw new ArgumentException(
                $"snapshot tick {snap.Tick} exceeds the 32-bit offset budget from start tick {_startTick}",
                nameof(snap));
        }

        var bytes = new byte[SnapshotWire.EncodedSize(snap)];
        SnapshotEncoder.Encode(snap, bytes);
        _frames.Add(((uint)offset, bytes));
    }

    public byte[] Build()
    {
        var totalLength = ReplayWire.HeaderBytes;
        foreach (var (_, bytes) in _frames)
        {
            totalLength += ReplayWire.FrameHeaderBytes + bytes.Length;
        }

        var output = new byte[totalLength];
        var span = output.AsSpan();

        ReplayWire.Magic.AsSpan().CopyTo(span);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], ReplayWire.Version);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)_tickRateHz);
        BinaryPrimitives.WriteUInt64LittleEndian(span[12..], (ulong)_startTick.Value);
        BinaryPrimitives.WriteUInt32LittleEndian(span[20..], (uint)_frames.Count);

        var cursor = ReplayWire.HeaderBytes;
        foreach (var (tickOffset, bytes) in _frames)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[cursor..], tickOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(span[(cursor + 4)..], (uint)bytes.Length);
            bytes.AsSpan().CopyTo(span[(cursor + ReplayWire.FrameHeaderBytes)..]);
            cursor += ReplayWire.FrameHeaderBytes + bytes.Length;
        }

        return output;
    }
}
