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
        var bytes = writer.Build();

        var result = ReplayReader.TryRead(bytes, out var header, out var frames);

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
        var bytes = writer.Build();

        var result = ReplayReader.TryRead(bytes, out var header, out var frames);
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

        var bytes = writer.Build();
        ReplayReader.TryRead(bytes, out var header, out var frames).Ok.Should().BeTrue();

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
        var bytes1 = w1.Build();

        var w2 = new ReplayWriter(60, new Tick(100));
        w2.RecordSnapshot(snap);
        var bytes2 = w2.Build();

        Sha256(bytes1).Should().Be(Sha256(bytes2));
    }

    [Fact]
    public void Read_rejects_bad_magic()
    {
        var bytes = new byte[ReplayWire.HeaderBytes];
        bytes[0] = (byte)'X';

        var result = ReplayReader.TryRead(bytes, out _, out var frames);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("magic");
        frames.Should().BeEmpty();
    }

    [Fact]
    public void Read_rejects_version_mismatch()
    {
        var writer = new ReplayWriter(60, Tick.Zero);
        var bytes = writer.Build();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 999u);

        var result = ReplayReader.TryRead(bytes, out _, out _);
        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("version");
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
        var bytes = writer.Build();

        bytes[0].Should().Be((byte)'S');
        bytes[1].Should().Be((byte)'V');
        bytes[2].Should().Be((byte)'O');
        bytes[3].Should().Be((byte)'R');
    }

    private static string Sha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
