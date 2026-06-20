using System;
using System.Numerics;
using System.Security.Cryptography;
using FluentAssertions;
using Garupan.Content;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Replay;
using Garupan.Sim.Snapshot;
using Garupan.Sim.Spawn;
using Opus.Foundation;
using Xunit;

namespace Garupan.Sim.Tests.Replay;

public sealed class ReplayRoundTripTests
{
    [Fact]
    public void Empty_replay_round_trips()
    {
        var writer = new ReplayWriter(tickRateHz: 60, startTick: Tick.Zero);
        var bytes = writer.Build(ReplayTestKeys.IntegrityKey);

        var result = ReplayReader.TryRead(
            bytes,
            ReplayTestKeys.IntegrityKey,
            out var header,
            out var frames);

        result.Ok.Should().BeTrue();
        header.TickRateHz.Should().Be(60);
        header.StartTick.Should().Be(Tick.Zero);
        header.FrameCount.Should().Be(0);
        frames.Should().BeEmpty();
    }

    [Fact]
    public void Single_frame_replay_round_trips_with_field_equality()
    {
        using var world = World.Create();
        TankSpawner.Spawn(world, TankRoster.VehicleMediumA, new Vector2(10f, -5f), 0.5f, Team.PlayerSchool, TankControl.Player);

        var snap = SnapshotCapture.Capture(world, new Tick(42));

        var writer = new ReplayWriter(60, new Tick(42));
        writer.RecordSnapshot(snap);
        var bytes = writer.Build(ReplayTestKeys.IntegrityKey);

        var result = ReplayReader.TryRead(
            bytes,
            ReplayTestKeys.IntegrityKey,
            out var header,
            out var frames);
        result.Ok.Should().BeTrue();
        header.FrameCount.Should().Be(1);
        frames.Should().HaveCount(1);
        frames[0].Tick.Should().Be(new Tick(42));
        frames[0].Snapshot.Entities.Should().BeEquivalentTo(snap.Entities, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void Multiple_frames_round_trip_in_order()
    {
        using var world = World.Create();
        TankSpawner.Spawn(world, TankRoster.VehicleMediumA, Vector2.Zero, 0f, Team.PlayerSchool, TankControl.Player);

        var writer = new ReplayWriter(60, new Tick(0));
        for (var t = 0; t < 5; t++)
        {
            // Mutate position so each snapshot is distinct — keeps the test honest if the
            // reader ever silently dropped duplicates.
            world.Raw.Query(
                new Arch.Core.QueryDescription().WithAll<Transform>(),
                (ref Transform tf) => tf.Position = new Vector2(t * 1f, 0f));
            writer.RecordSnapshot(SnapshotCapture.Capture(world, new Tick(t)));
        }

        var bytes = writer.Build(ReplayTestKeys.IntegrityKey);
        ReplayReader.TryRead(
            bytes,
            ReplayTestKeys.IntegrityKey,
            out var header,
            out var frames).Ok.Should().BeTrue();

        header.FrameCount.Should().Be(5);
        for (var i = 0; i < 5; i++)
        {
            frames[i].Tick.Should().Be(new Tick(i));
            frames[i].Snapshot.Entities.Should().HaveCount(1);
            frames[i].Snapshot.Entities[0].Position.X.Should().Be(i * 1f);
        }
    }

    [Fact]
    public void Replay_bytes_are_deterministic_for_identical_inputs()
    {
        // Golden-hash determinism check: the same captured world state must produce the
        // same byte stream regardless of how many times we encode it. Replay regressions
        // (rounding, archetype ordering, etc.) show up as a hash mismatch.
        using var world = World.Create();
        TankSpawner.Spawn(world, TankRoster.VehicleMediumA, new Vector2(7f, 3f), 1.5f, Team.PlayerSchool, TankControl.Player);

        var snap = SnapshotCapture.Capture(world, new Tick(100));

        var w1 = new ReplayWriter(60, new Tick(100));
        w1.RecordSnapshot(snap);
        var bytes1 = w1.Build(ReplayTestKeys.IntegrityKey);

        var w2 = new ReplayWriter(60, new Tick(100));
        w2.RecordSnapshot(snap);
        var bytes2 = w2.Build(ReplayTestKeys.IntegrityKey);

        Sha256(bytes1).Should().Be(Sha256(bytes2));
    }

    [Fact]
    public void Read_rejects_bad_magic()
    {
        var writer = new ReplayWriter(60, Tick.Zero);
        var bytes = writer.Build(ReplayTestKeys.IntegrityKey);
        bytes[0] = (byte)'X';

        var result = ReplayReader.TryRead(
            bytes,
            ReplayTestKeys.IntegrityKey,
            out _,
            out var frames);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("authentication");
        frames.Should().BeEmpty();
    }

    [Fact]
    public void Read_rejects_version_mismatch()
    {
        var writer = new ReplayWriter(60, Tick.Zero);
        var bytes = writer.Build(ReplayTestKeys.IntegrityKey);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 999u);

        var result = ReplayReader.TryRead(
            bytes,
            ReplayTestKeys.IntegrityKey,
            out _,
            out _);
        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("authentication");
    }

    [Fact]
    public void Recording_a_snapshot_with_earlier_tick_throws()
    {
        var writer = new ReplayWriter(60, new Tick(10));
        var snap = new WorldSnapshot(new Tick(5), Array.Empty<EntitySnapshot>(), Array.Empty<ProjectileSnapshot>());

        var act = () => writer.RecordSnapshot(snap);
        act.Should().Throw<ArgumentException>().WithMessage("*precedes*");
    }

    [Fact]
    public void Magic_prefix_is_svor_ascii()
    {
        var writer = new ReplayWriter(60, Tick.Zero);
        var bytes = writer.Build(ReplayTestKeys.IntegrityKey);

        bytes[0].Should().Be((byte)'S');
        bytes[1].Should().Be((byte)'V');
        bytes[2].Should().Be((byte)'O');
        bytes[3].Should().Be((byte)'R');
    }

    [Fact]
    public void Read_rejects_a_replay_signed_by_another_install()
    {
        var writer = new ReplayWriter(60, Tick.Zero);
        var bytes = writer.Build(ReplayTestKeys.IntegrityKey);
        var otherKey = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("other-install"));

        var result = ReplayReader.TryRead(bytes, otherKey, out _, out _);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("authentication");
    }

    [Fact]
    public void Read_rejects_a_tampered_replay_body()
    {
        var writer = new ReplayWriter(60, Tick.Zero);
        var bytes = writer.Build(ReplayTestKeys.IntegrityKey);
        bytes[ReplayWire.HeaderBytes - 1] ^= 0x80;

        var result = ReplayReader.TryRead(
            bytes,
            ReplayTestKeys.IntegrityKey,
            out _,
            out _);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("authentication");
    }

    [Fact]
    public void Writer_rejects_negative_start_tick()
    {
        var act = () => new ReplayWriter(60, new Tick(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Writer_rejects_tick_rate_above_wire_limit()
    {
        var act = () => new ReplayWriter(
            ReplayWire.MaximumTickRateHz + 1,
            Tick.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Writer_rejects_out_of_order_snapshot_ticks()
    {
        var writer = new ReplayWriter(60, Tick.Zero);
        writer.RecordSnapshot(EmptySnapshot(new Tick(2)));

        var act = () => writer.RecordSnapshot(EmptySnapshot(new Tick(1)));

        act.Should().Throw<ArgumentException>();
    }

    private static string Sha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static WorldSnapshot EmptySnapshot(Tick tick) => new(
        tick,
        Array.Empty<EntitySnapshot>(),
        Array.Empty<ProjectileSnapshot>());
}
