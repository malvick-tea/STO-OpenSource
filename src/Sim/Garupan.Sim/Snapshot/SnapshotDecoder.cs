using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Numerics;
using Garupan.Sim.Components;
using Opus.Foundation;

namespace Garupan.Sim.Snapshot;

/// <summary>
/// Parses a wire-format byte sequence (see <see cref="SnapshotWire"/>) back into a
/// <see cref="WorldSnapshot"/>. Rejects trailing bytes, truncated input, bad magic,
/// version mismatches, invalid values, and declared counts that exceed the buffer.
///
/// On failure <paramref name="snap"/> is set to an empty snapshot — a partial parse
/// must never leak to the caller because a half-filled snapshot looks identical to a
/// successful one from a row-count glance, which is the kind of bug that surfaces
/// months later as a desynced replay.
///
/// Ported from <c>svo::protocol::decode(span, WorldSnapshot&amp;)</c>.
/// </summary>
public static class SnapshotDecoder
{
    public static SnapshotCodecResult TryDecode(ReadOnlySpan<byte> bytes, out WorldSnapshot snap)
    {
        snap = Empty;

        if (bytes.Length < SnapshotWire.HeaderBytes
            || bytes.Length > SnapshotWire.MaxEncodedBytes)
        {
            return SnapshotCodecResult.Fail("snapshot: truncated header");
        }

        if (!bytes[..4].SequenceEqual(SnapshotWire.Magic))
        {
            return SnapshotCodecResult.Fail("snapshot: bad magic");
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        if (version != SnapshotWire.ProtocolVersion)
        {
            return SnapshotCodecResult.Fail("snapshot: protocol version mismatch");
        }

        var tickValue = BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]);
        if (tickValue > long.MaxValue)
        {
            return SnapshotCodecResult.Fail("snapshot: tick exceeds the supported range");
        }

        var entityCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]);
        if (entityCount > SnapshotWire.MaxEntities)
        {
            return SnapshotCodecResult.Fail("snapshot: entity count exceeds safety limit");
        }

        // Multiply in long to avoid uint32 overflow before the bounds compare.
        var afterEntities = SnapshotWire.HeaderBytes + ((long)entityCount * SnapshotWire.EntityBytes);
        if (bytes.Length < afterEntities)
        {
            return SnapshotCodecResult.Fail("snapshot: declared entity count exceeds buffer");
        }

        var entities = new List<EntitySnapshot>((int)entityCount);
        var cursor = SnapshotWire.HeaderBytes;
        for (var i = 0u; i < entityCount; i++)
        {
            var entityId = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(cursor + 0)..]);
            var stateFlags = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(cursor + 20)..]);
            if (entityId > int.MaxValue
                || (stateFlags & ~(uint)EntityStateFlags.KnockedOut) != 0)
            {
                return SnapshotCodecResult.Fail("snapshot: entity row contains invalid identity or flags");
            }

            entities.Add(new EntitySnapshot(
                Id: (int)entityId,
                Position: new Vector2(
                    BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 4)..]),
                    BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 8)..])),
                YawRadians: BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 12)..]),
                TurretYawRadians: BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 16)..]),
                StateFlags: (EntityStateFlags)stateFlags,
                BarrelPitchRadians: BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 24)..]),
                MinBarrelPitchRadians: BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 28)..]),
                MaxBarrelPitchRadians: BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 32)..]),
                GunRecoilTravelMeters: BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 36)..])));
            if (!SnapshotValuesAreFinite(entities[^1]))
            {
                return SnapshotCodecResult.Fail("snapshot: entity row contains non-finite values");
            }

            cursor += SnapshotWire.EntityBytes;
        }

        // Projectile section. The count prefix sits at the boundary; if the buffer
        // can't even hold the prefix we reject without producing a partial mix.
        if (bytes.Length < afterEntities + SnapshotWire.ProjectileCountBytes)
        {
            return SnapshotCodecResult.Fail("snapshot: truncated projectile-section header");
        }

        var projectileCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[cursor..]);
        if (projectileCount > SnapshotWire.MaxProjectiles)
        {
            return SnapshotCodecResult.Fail("snapshot: projectile count exceeds safety limit");
        }

        cursor += SnapshotWire.ProjectileCountBytes;

        var projectileRequired = afterEntities
            + SnapshotWire.ProjectileCountBytes
            + ((long)projectileCount * SnapshotWire.ProjectileBytes);
        if (bytes.Length < projectileRequired)
        {
            return SnapshotCodecResult.Fail("snapshot: declared projectile count exceeds buffer");
        }

        var projectiles = new List<ProjectileSnapshot>((int)projectileCount);
        for (var i = 0u; i < projectileCount; i++)
        {
            // Family is a single byte. The codec is transparent — out-of-range values
            // land as an out-of-range enumerator that the consumer treats as the
            // fallback (matches C++ behaviour).
            var familyByte = bytes[cursor + 20];
            var projectileId = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(cursor + 0)..]);
            var ownerEntityId = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(cursor + 45)..]);
            if (projectileId > int.MaxValue || ownerEntityId > int.MaxValue)
            {
                return SnapshotCodecResult.Fail("snapshot: projectile row contains invalid identity");
            }

            projectiles.Add(new ProjectileSnapshot(
                Id: (int)projectileId,
                Position: new Vector2(
                    BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 4)..]),
                    BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 8)..])),
                Velocity: new Vector2(
                    BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 12)..]),
                    BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 16)..])),
                Family: (AmmoType)familyByte,
                VisualHeightMeters: BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 21)..]),
                VerticalVelocityMps: BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 25)..]),
                DistanceTravelledMeters: BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 29)..]),
                LaunchPosition: new Vector2(
                    BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 33)..]),
                    BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 37)..])),
                LaunchVisualHeightMeters: BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 41)..]),
                OwnerEntityId: (int)ownerEntityId));
            if (!SnapshotValuesAreFinite(projectiles[^1]))
            {
                return SnapshotCodecResult.Fail("snapshot: projectile row contains non-finite values");
            }

            cursor += SnapshotWire.ProjectileBytes;
        }

        var propResult = TryDecodeProps(bytes, ref cursor, projectileRequired, out var props);
        if (!propResult.Ok)
        {
            return propResult;
        }

        if (cursor != bytes.Length)
        {
            return SnapshotCodecResult.Fail("snapshot: trailing bytes");
        }

        snap = new WorldSnapshot(new Tick((long)tickValue), entities, projectiles) { Props = props };
        return SnapshotCodecResult.Success;
    }

    /// <summary>Reads the trailing felled-prop section. Like the projectile section it is a u32
    /// count then fixed-size rows; an absent or truncated section is rejected so a half-parsed
    /// snapshot never reaches the caller. State is a single byte — an out-of-range value lands as
    /// an out-of-range enumerator the consumer treats as standing (matches the projectile-family
    /// tolerance).</summary>
    private static SnapshotCodecResult TryDecodeProps(
        ReadOnlySpan<byte> bytes,
        ref int cursor,
        long afterProjectiles,
        out IReadOnlyList<PropSnapshot> props)
    {
        props = Array.Empty<PropSnapshot>();
        if (bytes.Length < afterProjectiles + SnapshotWire.PropCountBytes)
        {
            return SnapshotCodecResult.Fail("snapshot: truncated prop-section header");
        }

        var propCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[cursor..]);
        if (propCount > SnapshotWire.MaxProps)
        {
            return SnapshotCodecResult.Fail("snapshot: prop count exceeds safety limit");
        }

        cursor += SnapshotWire.PropCountBytes;

        var required = afterProjectiles + SnapshotWire.PropCountBytes + ((long)propCount * SnapshotWire.PropBytes);
        if (bytes.Length < required)
        {
            return SnapshotCodecResult.Fail("snapshot: declared prop count exceeds buffer");
        }

        if (propCount == 0)
        {
            return SnapshotCodecResult.Success;
        }

        var rows = new List<PropSnapshot>((int)propCount);
        for (var i = 0u; i < propCount; i++)
        {
            var propId = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(cursor + 0)..]);
            if (propId > int.MaxValue)
            {
                return SnapshotCodecResult.Fail("snapshot: prop row contains invalid identity");
            }

            rows.Add(new PropSnapshot(
                PropId: (int)propId,
                State: (PropState)bytes[cursor + 4],
                FallYawRadians: BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 5)..]),
                ToppleSeconds: BinaryPrimitives.ReadSingleLittleEndian(bytes[(cursor + 9)..])));
            if (!float.IsFinite(rows[^1].FallYawRadians)
                || !float.IsFinite(rows[^1].ToppleSeconds)
                || !Enum.IsDefined(rows[^1].State))
            {
                return SnapshotCodecResult.Fail("snapshot: prop row contains invalid values");
            }

            cursor += SnapshotWire.PropBytes;
        }

        props = rows;
        return SnapshotCodecResult.Success;
    }

    private static readonly WorldSnapshot Empty = new(
        Tick.Zero,
        Array.Empty<EntitySnapshot>(),
        Array.Empty<ProjectileSnapshot>());

    private static bool SnapshotValuesAreFinite(EntitySnapshot row) =>
        float.IsFinite(row.Position.X)
        && float.IsFinite(row.Position.Y)
        && float.IsFinite(row.YawRadians)
        && float.IsFinite(row.TurretYawRadians)
        && float.IsFinite(row.BarrelPitchRadians)
        && float.IsFinite(row.MinBarrelPitchRadians)
        && float.IsFinite(row.MaxBarrelPitchRadians)
        && float.IsFinite(row.GunRecoilTravelMeters);

    private static bool SnapshotValuesAreFinite(ProjectileSnapshot row) =>
        float.IsFinite(row.Position.X)
        && float.IsFinite(row.Position.Y)
        && float.IsFinite(row.Velocity.X)
        && float.IsFinite(row.Velocity.Y)
        && float.IsFinite(row.VisualHeightMeters)
        && float.IsFinite(row.VerticalVelocityMps)
        && float.IsFinite(row.DistanceTravelledMeters)
        && float.IsFinite(row.LaunchPosition.X)
        && float.IsFinite(row.LaunchPosition.Y)
        && float.IsFinite(row.LaunchVisualHeightMeters)
        && Enum.IsDefined(row.Family);
}
