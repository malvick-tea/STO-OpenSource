using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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
    private long? _lastRecordedTick;

    public ReplayWriter(int tickRateHz, Tick startTick)
    {
        if (tickRateHz is <= 0 or > ReplayWire.MaximumTickRateHz)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tickRateHz),
                tickRateHz,
                $"Tick rate must be in [1,{ReplayWire.MaximumTickRateHz}].");
        }

        if (startTick.Value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startTick),
                startTick,
                "Replay start tick must be non-negative.");
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

        if (_frames.Count >= ReplayWire.MaxReplayFrames)
        {
            throw new InvalidOperationException(
                $"Replay frame count exceeds {ReplayWire.MaxReplayFrames}.");
        }

        if (_lastRecordedTick is long lastTick && snap.Tick.Value <= lastTick)
        {
            throw new ArgumentException(
                $"snapshot tick {snap.Tick} is not later than the previous tick {lastTick}",
                nameof(snap));
        }

        var bytes = new byte[SnapshotWire.EncodedSize(snap)];
        SnapshotEncoder.Encode(snap, bytes);
        _frames.Add(((uint)offset, bytes));
        _lastRecordedTick = snap.Tick.Value;
    }

    public byte[] Build(ReadOnlySpan<byte> authenticationKey)
    {
        ValidateAuthenticationKey(authenticationKey);
        long payloadLength = ReplayWire.HeaderBytes;
        foreach (var (_, bytes) in _frames)
        {
            payloadLength = checked(payloadLength + ReplayWire.FrameHeaderBytes + bytes.Length);
        }

        var totalLength = checked(payloadLength + ReplayWire.AuthenticationTagBytes);
        if (totalLength > ReplayWire.MaxReplayBytes)
        {
            throw new InvalidOperationException(
                $"Replay exceeds the {ReplayWire.MaxReplayBytes}-byte limit.");
        }

        var output = new byte[checked((int)totalLength)];
        var payload = output.AsSpan(0, checked((int)payloadLength));

        ReplayWire.Magic.AsSpan().CopyTo(payload);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[4..], ReplayWire.Version);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[8..], (uint)_tickRateHz);
        BinaryPrimitives.WriteUInt64LittleEndian(payload[12..], (ulong)_startTick.Value);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[20..], (uint)_frames.Count);

        var cursor = ReplayWire.HeaderBytes;
        foreach (var (tickOffset, bytes) in _frames)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(payload[cursor..], tickOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(payload[(cursor + 4)..], (uint)bytes.Length);
            bytes.AsSpan().CopyTo(payload[(cursor + ReplayWire.FrameHeaderBytes)..]);
            cursor += ReplayWire.FrameHeaderBytes + bytes.Length;
        }

        HMACSHA256.HashData(
            authenticationKey,
            payload,
            output.AsSpan(checked((int)payloadLength), ReplayWire.AuthenticationTagBytes));
        return output;
    }

    private static void ValidateAuthenticationKey(ReadOnlySpan<byte> authenticationKey)
    {
        if (authenticationKey.Length < ReplayWire.MinimumAuthenticationKeyBytes)
        {
            throw new ArgumentException(
                $"Replay authentication key must contain at least {ReplayWire.MinimumAuthenticationKeyBytes} bytes.",
                nameof(authenticationKey));
        }
    }
}
