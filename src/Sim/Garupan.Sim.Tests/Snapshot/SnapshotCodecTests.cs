using System;
using System.Numerics;
using FluentAssertions;
using Garupan.Sim.Components;
using Garupan.Sim.Snapshot;
using Opus.Foundation;
using Xunit;
using AmmoType = Garupan.Sim.Components.AmmoType;

namespace Garupan.Sim.Tests.Snapshot;

public sealed class SnapshotCodecTests
{
    [Fact]
    public void Empty_snapshot_round_trips_through_encode_decode()
    {
        var original = new WorldSnapshot(new Tick(0), Array.Empty<EntitySnapshot>(), Array.Empty<ProjectileSnapshot>());
        var buffer = new byte[SnapshotWire.EncodedSize(original)];

        var written = SnapshotEncoder.Encode(original, buffer);
        written.Should().Be(buffer.Length);

        var result = SnapshotDecoder.TryDecode(buffer, out var decoded);
        result.Ok.Should().BeTrue();
        decoded.Tick.Should().Be(new Tick(0));
        decoded.Entities.Should().BeEmpty();
        decoded.Projectiles.Should().BeEmpty();
    }

    [Fact]
    public void Snapshot_with_entities_and_projectiles_round_trips()
    {
        var original = new WorldSnapshot(
            new Tick(1234),
            new[]
            {
                new EntitySnapshot(Id: 1, Position: new Vector2(10f, -5f), YawRadians: 1.5f, TurretYawRadians: 0.25f, StateFlags: EntityStateFlags.None, BarrelPitchRadians: 0.2f, MinBarrelPitchRadians: -0.1f, MaxBarrelPitchRadians: 0.3f, GunRecoilTravelMeters: 0.25f),
                new EntitySnapshot(Id: 2, Position: new Vector2(-8f, 12f), YawRadians: -0.5f, TurretYawRadians: 3.14f, StateFlags: EntityStateFlags.KnockedOut),
            },
            new[]
            {
                new ProjectileSnapshot(Id: 100, Position: new Vector2(3f, 4f), Velocity: new Vector2(500f, 0f), Family: AmmoType.AP, VisualHeightMeters: 2.25f, VerticalVelocityMps: -4f, DistanceTravelledMeters: 81f, LaunchPosition: new Vector2(1f, 2f), LaunchVisualHeightMeters: 1.75f, OwnerEntityId: 7),
                new ProjectileSnapshot(Id: 101, Position: new Vector2(7f, 8f), Velocity: new Vector2(-200f, 300f), Family: AmmoType.HEAT),
            });

        var buffer = new byte[SnapshotWire.EncodedSize(original)];
        SnapshotEncoder.Encode(original, buffer);

        var result = SnapshotDecoder.TryDecode(buffer, out var decoded);
        result.Ok.Should().BeTrue();

        decoded.Tick.Should().Be(original.Tick);
        decoded.Entities.Should().BeEquivalentTo(original.Entities, opts => opts.WithStrictOrdering());
        decoded.Projectiles.Should().BeEquivalentTo(original.Projectiles, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void Snapshot_with_felled_props_round_trips()
    {
        var original = new WorldSnapshot(
            new Tick(42),
            Array.Empty<EntitySnapshot>(),
            Array.Empty<ProjectileSnapshot>())
        {
            Props = new[]
            {
                new PropSnapshot(PropId: 17, State: PropState.Toppling, FallYawRadians: 1.25f, ToppleSeconds: 0.3f),
                new PropSnapshot(PropId: 1593, State: PropState.Fallen, FallYawRadians: -2.7f, ToppleSeconds: 0.8f),
                new PropSnapshot(PropId: 4, State: PropState.Broken, FallYawRadians: 0f, ToppleSeconds: 0f),
            },
        };

        var buffer = new byte[SnapshotWire.EncodedSize(original)];
        SnapshotEncoder.Encode(original, buffer).Should().Be(buffer.Length);

        var result = SnapshotDecoder.TryDecode(buffer, out var decoded);
        result.Ok.Should().BeTrue();
        decoded.Props.Should().BeEquivalentTo(original.Props, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void Empty_snapshot_carries_no_props()
    {
        var snap = new WorldSnapshot(new Tick(0), Array.Empty<EntitySnapshot>(), Array.Empty<ProjectileSnapshot>());
        var buffer = new byte[SnapshotWire.EncodedSize(snap)];
        SnapshotEncoder.Encode(snap, buffer);

        SnapshotDecoder.TryDecode(buffer, out var decoded).Ok.Should().BeTrue();
        decoded.Props.Should().BeEmpty();
    }

    [Fact]
    public void Magic_prefix_is_svos_ascii()
    {
        var snap = new WorldSnapshot(new Tick(1), Array.Empty<EntitySnapshot>(), Array.Empty<ProjectileSnapshot>());
        var buffer = new byte[SnapshotWire.EncodedSize(snap)];
        SnapshotEncoder.Encode(snap, buffer);

        buffer[0].Should().Be((byte)'S');
        buffer[1].Should().Be((byte)'V');
        buffer[2].Should().Be((byte)'O');
        buffer[3].Should().Be((byte)'S');
    }

    [Fact]
    public void Decode_rejects_truncated_header()
    {
        var bytes = new byte[10]; // less than the 20-byte header

        var result = SnapshotDecoder.TryDecode(bytes, out var decoded);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("truncated");
        decoded.Entities.Should().BeEmpty();
        decoded.Projectiles.Should().BeEmpty();
    }

    [Fact]
    public void Decode_rejects_bad_magic()
    {
        var bytes = new byte[SnapshotWire.HeaderBytes];
        bytes[0] = (byte)'X'; // not 'S'
        bytes[1] = (byte)'X';
        bytes[2] = (byte)'X';
        bytes[3] = (byte)'X';

        var result = SnapshotDecoder.TryDecode(bytes, out _);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("magic");
    }

    [Fact]
    public void Decode_rejects_wrong_protocol_version()
    {
        var snap = new WorldSnapshot(new Tick(1), Array.Empty<EntitySnapshot>(), Array.Empty<ProjectileSnapshot>());
        var buffer = new byte[SnapshotWire.EncodedSize(snap)];
        SnapshotEncoder.Encode(snap, buffer);

        // Stomp the version field (offset 4) with a value the decoder will reject.
        buffer[4] = 99;
        buffer[5] = 0;
        buffer[6] = 0;
        buffer[7] = 0;

        var result = SnapshotDecoder.TryDecode(buffer, out _);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("version");
    }

    [Fact]
    public void Decode_rejects_declared_entity_count_exceeding_buffer()
    {
        var buffer = new byte[SnapshotWire.HeaderBytes];
        // Valid magic + version, but declare 5 entities while the buffer holds zero rows.
        SnapshotWire.Magic.CopyTo(buffer, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4), SnapshotWire.ProtocolVersion);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16), 5u);

        var result = SnapshotDecoder.TryDecode(buffer, out var decoded);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("entity");
        decoded.Entities.Should().BeEmpty();
    }

    [Fact]
    public void Encoded_size_matches_actual_bytes_written()
    {
        var snap = new WorldSnapshot(
            new Tick(7),
            new[]
            {
                new EntitySnapshot(Id: 1, Position: Vector2.Zero, YawRadians: 0f, TurretYawRadians: 0f, StateFlags: EntityStateFlags.None),
                new EntitySnapshot(Id: 2, Position: Vector2.One, YawRadians: 1f, TurretYawRadians: 2f, StateFlags: EntityStateFlags.KnockedOut),
                new EntitySnapshot(Id: 3, Position: -Vector2.One, YawRadians: -1f, TurretYawRadians: -2f, StateFlags: EntityStateFlags.None),
            },
            new[]
            {
                new ProjectileSnapshot(Id: 50, Position: Vector2.Zero, Velocity: Vector2.UnitX, Family: AmmoType.APCR),
            })
        {
            Props = new[]
            {
                new PropSnapshot(PropId: 9, State: PropState.Fallen, FallYawRadians: 0.5f, ToppleSeconds: 0.8f),
                new PropSnapshot(PropId: 12, State: PropState.Broken, FallYawRadians: 0f, ToppleSeconds: 0f),
            },
        };

        var expected = SnapshotWire.HeaderBytes
            + 3 * SnapshotWire.EntityBytes
            + SnapshotWire.ProjectileCountBytes
            + 1 * SnapshotWire.ProjectileBytes
            + SnapshotWire.PropCountBytes
            + 2 * SnapshotWire.PropBytes;

        SnapshotWire.EncodedSize(snap).Should().Be(expected);

        var buffer = new byte[expected];
        SnapshotEncoder.Encode(snap, buffer).Should().Be(expected);
    }

    [Fact]
    public void Decode_rejects_trailing_bytes_past_the_declared_payload()
    {
        var snap = new WorldSnapshot(new Tick(42), Array.Empty<EntitySnapshot>(), Array.Empty<ProjectileSnapshot>());
        var exact = SnapshotWire.EncodedSize(snap);
        var buffer = new byte[exact + 16]; // 16 trailing bytes of garbage
        SnapshotEncoder.Encode(snap, buffer);

        var result = SnapshotDecoder.TryDecode(buffer, out _);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("trailing");
    }

    [Fact]
    public void Encode_throws_when_buffer_is_too_small()
    {
        var snap = new WorldSnapshot(new Tick(1), Array.Empty<EntitySnapshot>(), Array.Empty<ProjectileSnapshot>());
        var tooSmall = new byte[SnapshotWire.EncodedSize(snap) - 1];

        var act = () => SnapshotEncoder.Encode(snap, tooSmall);
        act.Should().Throw<ArgumentException>().WithMessage("*too small*");
    }
}
