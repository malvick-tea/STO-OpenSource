using System;
using System.Buffers.Binary;

namespace Garupan.Sim.Snapshot;

/// <summary>
/// Writes a <see cref="WorldSnapshot"/> into a caller-provided byte buffer in the wire
/// format described in <see cref="SnapshotWire"/>. Allocation-free in the hot path —
/// the buffer is the caller's, sized via <see cref="SnapshotWire.EncodedSize"/>.
///
/// Endianness: every multi-byte field is little-endian, asserted by the C++ contract
/// and by <see cref="BinaryPrimitives.WriteUInt32LittleEndian(System.Span{byte},uint)"/>.
/// The wire and host bit patterns are identical on every supported platform (Windows /
/// Linux on x86_64, mobile arm64) but routing through the BinaryPrimitives helpers
/// keeps the codec correct if that ever stops being true.
///
/// Ported from <c>svo::protocol::encode(WorldSnapshot, span)</c>.
/// </summary>
public static class SnapshotEncoder
{
    /// <summary>
    /// Encode <paramref name="snap"/> into <paramref name="buffer"/>. The buffer must be
    /// at least <see cref="SnapshotWire.EncodedSize"/> bytes wide; <see cref="ArgumentException"/>
    /// otherwise. Returns the number of bytes written, equal to <c>EncodedSize(snap)</c>.
    /// </summary>
    public static int Encode(WorldSnapshot snap, Span<byte> buffer)
    {
        var required = SnapshotWire.EncodedSize(snap);
        if (buffer.Length < required)
        {
            throw new ArgumentException(
                $"snapshot encode buffer too small: need {required}, got {buffer.Length}",
                nameof(buffer));
        }

        // Magic.
        SnapshotWire.Magic.AsSpan().CopyTo(buffer);

        // Protocol version.
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[4..], SnapshotWire.ProtocolVersion);

        // Tick. The integer cast widens long → ulong without sign-extension; tick is
        // monotonic non-negative so the high bit is never set in practice.
        BinaryPrimitives.WriteUInt64LittleEndian(buffer[8..], (ulong)snap.Tick.Value);

        // Entity count. The same uint32 width the C++ wire uses — the ECS upper bound
        // is well below 2^32 so the cast never truncates.
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[16..], (uint)snap.Entities.Count);

        var cursor = SnapshotWire.HeaderBytes;
        for (var i = 0; i < snap.Entities.Count; i++)
        {
            var row = snap.Entities[i];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer[(cursor + 0)..], (uint)row.Id);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 4)..], row.Position.X);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 8)..], row.Position.Y);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 12)..], row.YawRadians);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 16)..], row.TurretYawRadians);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer[(cursor + 20)..], (uint)row.StateFlags);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 24)..], row.BarrelPitchRadians);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 28)..], row.MinBarrelPitchRadians);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 32)..], row.MaxBarrelPitchRadians);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 36)..], row.GunRecoilTravelMeters);
            cursor += SnapshotWire.EntityBytes;
        }

        // Projectile section: u32 count followed by fixed-size rows.
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[cursor..], (uint)snap.Projectiles.Count);
        cursor += SnapshotWire.ProjectileCountBytes;

        for (var i = 0; i < snap.Projectiles.Count; i++)
        {
            var row = snap.Projectiles[i];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer[(cursor + 0)..], (uint)row.Id);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 4)..], row.Position.X);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 8)..], row.Position.Y);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 12)..], row.Velocity.X);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 16)..], row.Velocity.Y);
            buffer[cursor + 20] = (byte)row.Family;
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 21)..], row.VisualHeightMeters);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 25)..], row.VerticalVelocityMps);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 29)..], row.DistanceTravelledMeters);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 33)..], row.LaunchPosition.X);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 37)..], row.LaunchPosition.Y);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 41)..], row.LaunchVisualHeightMeters);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer[(cursor + 45)..], (uint)row.OwnerEntityId);
            cursor += SnapshotWire.ProjectileBytes;
        }

        // Felled-prop section: u32 count followed by fixed-size rows. Empty (count 0) for every
        // tick with no broken clutter and for the prop-free determinism / replay scenarios.
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[cursor..], (uint)snap.Props.Count);
        cursor += SnapshotWire.PropCountBytes;

        for (var i = 0; i < snap.Props.Count; i++)
        {
            var row = snap.Props[i];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer[(cursor + 0)..], (uint)row.PropId);
            buffer[cursor + 4] = (byte)row.State;
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 5)..], row.FallYawRadians);
            BinaryPrimitives.WriteSingleLittleEndian(buffer[(cursor + 9)..], row.ToppleSeconds);
            cursor += SnapshotWire.PropBytes;
        }

        return cursor;
    }
}
