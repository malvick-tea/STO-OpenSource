using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Numerics;
using FluentAssertions;
using Garupan.Sim.Components;
using Garupan.Sim.Snapshot;
using Opus.Foundation;
using Xunit;
using AmmoType = Garupan.Sim.Components.AmmoType;

namespace Garupan.Sim.Tests.Snapshot;

/// <summary>
/// Robustness-tier coverage for the snapshot codec. <see cref="SnapshotCodecTests"/>
/// pins the happy path + the obvious malformed-input rejections; this suite covers
/// the corners that surface only at scale (large rows, every ammo type, float boundary
/// values, projectile-section truncation branches the decoder ships but the original
/// happy-path tests never reached).
/// </summary>
public sealed class SnapshotCodecRobustnessTests
{
    [Fact]
    public void Decode_rejects_truncated_projectile_section_header()
    {
        // Construct a valid header declaring zero entities but stop before the 4-byte
        // projectile-count prefix — exercises the truncation guard between the entity
        // tail and the projectile-section header.
        var buffer = new byte[SnapshotWire.HeaderBytes];
        SnapshotWire.Magic.CopyTo(buffer, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4), SnapshotWire.ProtocolVersion);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(8), 1ul);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16), 0u);

        var result = SnapshotDecoder.TryDecode(buffer, out var decoded);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("projectile-section header");
        decoded.Entities.Should().BeEmpty();
        decoded.Projectiles.Should().BeEmpty();
    }

    [Fact]
    public void Decode_rejects_declared_projectile_count_exceeding_buffer()
    {
        var buffer = new byte[SnapshotWire.HeaderBytes + SnapshotWire.ProjectileCountBytes];
        SnapshotWire.Magic.CopyTo(buffer, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4), SnapshotWire.ProtocolVersion);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(8), 1ul);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16), 0u);

        // Declare 3 projectiles without writing their payload.
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(SnapshotWire.HeaderBytes), 3u);

        var result = SnapshotDecoder.TryDecode(buffer, out var decoded);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("projectile");
        decoded.Projectiles.Should().BeEmpty();
    }

    [Fact]
    public void Decode_rejects_truncated_prop_section_header()
    {
        // Valid header + an empty projectile section, but stop before the 4-byte prop-count
        // prefix — exercises the guard between the projectile tail and the prop-section header.
        var buffer = new byte[SnapshotWire.HeaderBytes + SnapshotWire.ProjectileCountBytes];
        SnapshotWire.Magic.CopyTo(buffer, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4), SnapshotWire.ProtocolVersion);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(8), 1ul);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16), 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(SnapshotWire.HeaderBytes), 0u);

        var result = SnapshotDecoder.TryDecode(buffer, out var decoded);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("prop-section header");
        decoded.Props.Should().BeEmpty();
    }

    [Fact]
    public void Decode_rejects_declared_prop_count_exceeding_buffer()
    {
        var buffer = new byte[SnapshotWire.HeaderBytes + SnapshotWire.ProjectileCountBytes + SnapshotWire.PropCountBytes];
        SnapshotWire.Magic.CopyTo(buffer, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4), SnapshotWire.ProtocolVersion);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(8), 1ul);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16), 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(SnapshotWire.HeaderBytes), 0u);

        // Declare 3 felled props without writing their payload.
        BinaryPrimitives.WriteUInt32LittleEndian(
            buffer.AsSpan(SnapshotWire.HeaderBytes + SnapshotWire.ProjectileCountBytes), 3u);

        var result = SnapshotDecoder.TryDecode(buffer, out var decoded);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("prop count");
        decoded.Props.Should().BeEmpty();
    }

    [Fact]
    public void Large_snapshot_round_trips_without_cursor_drift()
    {
        const int entityCount = 256;
        const int projectileCount = 128;

        var entities = new List<EntitySnapshot>(entityCount);
        for (var i = 0; i < entityCount; i++)
        {
            entities.Add(new EntitySnapshot(
                Id: i + 1,
                Position: new Vector2(i * 0.5f, -i * 0.25f),
                YawRadians: i * 0.01f,
                TurretYawRadians: -i * 0.02f,
                StateFlags: (i & 1) == 0 ? EntityStateFlags.None : EntityStateFlags.KnockedOut,
                BarrelPitchRadians: i * 0.001f,
                MinBarrelPitchRadians: -0.1f - (i * 0.001f),
                MaxBarrelPitchRadians: 0.2f + (i * 0.001f)));
        }

        var projectiles = new List<ProjectileSnapshot>(projectileCount);
        for (var i = 0; i < projectileCount; i++)
        {
            projectiles.Add(new ProjectileSnapshot(
                Id: 1000 + i,
                Position: new Vector2(-i * 0.1f, i * 0.3f),
                Velocity: new Vector2(500f - i, i * 2f),
                Family: AmmoType.AP,
                VisualHeightMeters: 2f + (i * 0.01f),
                VerticalVelocityMps: -i * 0.1f,
                DistanceTravelledMeters: i * 3f));
        }

        var original = new WorldSnapshot(new Tick(999999), entities, projectiles);
        var buffer = new byte[SnapshotWire.EncodedSize(original)];
        SnapshotEncoder.Encode(original, buffer).Should().Be(buffer.Length);

        var result = SnapshotDecoder.TryDecode(buffer, out var decoded);
        result.Ok.Should().BeTrue();
        decoded.Tick.Should().Be(original.Tick);
        decoded.Entities.Should().HaveCount(entityCount);
        decoded.Projectiles.Should().HaveCount(projectileCount);

        // Spot-check first / last / mid rows — cursor drift always shows up at the
        // boundary or in the middle, not at the start.
        AssertEntityRoundTrip(original.Entities[0], decoded.Entities[0]);
        AssertEntityRoundTrip(original.Entities[entityCount / 2], decoded.Entities[entityCount / 2]);
        AssertEntityRoundTrip(original.Entities[entityCount - 1], decoded.Entities[entityCount - 1]);

        AssertProjectileRoundTrip(original.Projectiles[0], decoded.Projectiles[0]);
        AssertProjectileRoundTrip(original.Projectiles[projectileCount / 2], decoded.Projectiles[projectileCount / 2]);
        AssertProjectileRoundTrip(original.Projectiles[projectileCount - 1], decoded.Projectiles[projectileCount - 1]);
    }

    [Theory]
    [InlineData(AmmoType.AP)]
    [InlineData(AmmoType.APCR)]
    [InlineData(AmmoType.HEAT)]
    [InlineData(AmmoType.HE)]
    public void Every_ammo_type_survives_round_trip(AmmoType family)
    {
        var snap = new WorldSnapshot(
            new Tick(1),
            Array.Empty<EntitySnapshot>(),
            new[]
            {
                new ProjectileSnapshot(
                    Id: 7,
                    Position: new Vector2(1f, 2f),
                    Velocity: new Vector2(3f, 4f),
                    Family: family),
            });

        var buffer = new byte[SnapshotWire.EncodedSize(snap)];
        SnapshotEncoder.Encode(snap, buffer);
        SnapshotDecoder.TryDecode(buffer, out var decoded).Ok.Should().BeTrue();

        decoded.Projectiles[0].Family.Should().Be(family);
    }

    [Theory]
    [InlineData(EntityStateFlags.None)]
    [InlineData(EntityStateFlags.KnockedOut)]
    public void Every_entity_state_flag_value_survives_round_trip(EntityStateFlags flags)
    {
        var snap = new WorldSnapshot(
            new Tick(1),
            new[]
            {
                new EntitySnapshot(Id: 1, Position: Vector2.Zero, YawRadians: 0f, TurretYawRadians: 0f, StateFlags: flags),
            },
            Array.Empty<ProjectileSnapshot>());

        var buffer = new byte[SnapshotWire.EncodedSize(snap)];
        SnapshotEncoder.Encode(snap, buffer);
        SnapshotDecoder.TryDecode(buffer, out var decoded).Ok.Should().BeTrue();

        decoded.Entities[0].StateFlags.Should().Be(flags);
    }

    [Fact]
    public void Float_boundary_values_survive_round_trip_exactly()
    {
        var snap = new WorldSnapshot(
            new Tick(1),
            new[]
            {
                new EntitySnapshot(
                    Id: 1,
                    Position: new Vector2(float.MinValue, float.MaxValue),
                    YawRadians: float.Epsilon,
                    TurretYawRadians: -float.Epsilon,
                    StateFlags: EntityStateFlags.None),
                new EntitySnapshot(
                    Id: 2,
                    Position: new Vector2(0f, -0f),
                    YawRadians: float.PositiveInfinity,
                    TurretYawRadians: float.NegativeInfinity,
                    StateFlags: EntityStateFlags.None),
            },
            Array.Empty<ProjectileSnapshot>());

        var buffer = new byte[SnapshotWire.EncodedSize(snap)];
        SnapshotEncoder.Encode(snap, buffer);
        SnapshotDecoder.TryDecode(buffer, out var decoded).Ok.Should().BeTrue();

        decoded.Entities[0].Position.X.Should().Be(float.MinValue);
        decoded.Entities[0].Position.Y.Should().Be(float.MaxValue);
        decoded.Entities[0].YawRadians.Should().Be(float.Epsilon);
        decoded.Entities[0].TurretYawRadians.Should().Be(-float.Epsilon);

        decoded.Entities[1].Position.X.Should().Be(0f);
        decoded.Entities[1].Position.Y.Should().Be(-0f);
        decoded.Entities[1].YawRadians.Should().Be(float.PositiveInfinity);
        decoded.Entities[1].TurretYawRadians.Should().Be(float.NegativeInfinity);
    }

    [Fact]
    public void NaN_survives_round_trip_as_NaN()
    {
        var snap = new WorldSnapshot(
            new Tick(1),
            new[]
            {
                new EntitySnapshot(
                    Id: 1,
                    Position: new Vector2(float.NaN, float.NaN),
                    YawRadians: float.NaN,
                    TurretYawRadians: float.NaN,
                    StateFlags: EntityStateFlags.None),
            },
            Array.Empty<ProjectileSnapshot>());

        var buffer = new byte[SnapshotWire.EncodedSize(snap)];
        SnapshotEncoder.Encode(snap, buffer);
        SnapshotDecoder.TryDecode(buffer, out var decoded).Ok.Should().BeTrue();

        // NaN != NaN by IEEE-754 convention, so direct equality fails. The codec contract
        // is bit-pattern preservation, which IsNaN verifies.
        float.IsNaN(decoded.Entities[0].Position.X).Should().BeTrue();
        float.IsNaN(decoded.Entities[0].Position.Y).Should().BeTrue();
        float.IsNaN(decoded.Entities[0].YawRadians).Should().BeTrue();
        float.IsNaN(decoded.Entities[0].TurretYawRadians).Should().BeTrue();
    }

    [Fact]
    public void Very_large_tick_value_survives_round_trip()
    {
        var snap = new WorldSnapshot(
            new Tick(long.MaxValue),
            Array.Empty<EntitySnapshot>(),
            Array.Empty<ProjectileSnapshot>());

        var buffer = new byte[SnapshotWire.EncodedSize(snap)];
        SnapshotEncoder.Encode(snap, buffer);
        SnapshotDecoder.TryDecode(buffer, out var decoded).Ok.Should().BeTrue();

        decoded.Tick.Value.Should().Be(long.MaxValue);
    }

    private static void AssertEntityRoundTrip(EntitySnapshot expected, EntitySnapshot actual)
    {
        actual.Id.Should().Be(expected.Id);
        actual.Position.X.Should().Be(expected.Position.X);
        actual.Position.Y.Should().Be(expected.Position.Y);
        actual.YawRadians.Should().Be(expected.YawRadians);
        actual.TurretYawRadians.Should().Be(expected.TurretYawRadians);
        actual.BarrelPitchRadians.Should().Be(expected.BarrelPitchRadians);
        actual.MinBarrelPitchRadians.Should().Be(expected.MinBarrelPitchRadians);
        actual.MaxBarrelPitchRadians.Should().Be(expected.MaxBarrelPitchRadians);
        actual.GunRecoilTravelMeters.Should().Be(expected.GunRecoilTravelMeters);
        actual.StateFlags.Should().Be(expected.StateFlags);
    }

    private static void AssertProjectileRoundTrip(ProjectileSnapshot expected, ProjectileSnapshot actual)
    {
        actual.Id.Should().Be(expected.Id);
        actual.Position.X.Should().Be(expected.Position.X);
        actual.Position.Y.Should().Be(expected.Position.Y);
        actual.Velocity.X.Should().Be(expected.Velocity.X);
        actual.Velocity.Y.Should().Be(expected.Velocity.Y);
        actual.Family.Should().Be(expected.Family);
        actual.VisualHeightMeters.Should().Be(expected.VisualHeightMeters);
        actual.VerticalVelocityMps.Should().Be(expected.VerticalVelocityMps);
        actual.DistanceTravelledMeters.Should().Be(expected.DistanceTravelledMeters);
        actual.OwnerEntityId.Should().Be(expected.OwnerEntityId);
    }
}
