using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Garupan.Client.Windows.Direct3D12.Composition.Models;
using Garupan.Content;
using Opus.Engine.Audio;
using Opus.Engine.Ui;
using Xunit;

namespace Garupan.Client.Windows.Direct3D12.Tests.Composition.Models;

public sealed class TankAudioTrackerTests
{
    private static readonly CameraView3D Camera =
        CameraView3D.LookAt(new Vector3(0f, 5f, -10f), Vector3.Zero);

    [Fact]
    public void Newly_observed_projectile_plays_gun_once()
    {
        var clock = new FakeClock();
        var sfx = new RecordingSfxPlayer();
        using var tracker = new TankAudioTracker(sfx, new RecordingLoopingSfxPlayer(), Profile, clock.Read);
        var projectile = new ProjectilePlacement(Vector3.One, Vector3.UnitX, 7, Vector3.Zero, OwnerEntityId: 1);

        tracker.Resolve(Plan(10, new[] { Self(Vector3.Zero) }, projectile));
        tracker.Resolve(Plan(11, new[] { Self(Vector3.Zero) }, projectile));

        sfx.Paths.Should().ContainSingle(path => path.EndsWith("/gun.ogg", StringComparison.Ordinal));
        sfx.Paths.Should().ContainSingle(path => path.EndsWith("/reload.ogg", StringComparison.Ordinal));
    }

    [Fact]
    public void Projectile_owned_by_another_tank_does_not_play_the_local_profile()
    {
        var clock = new FakeClock();
        var sfx = new RecordingSfxPlayer();
        using var tracker = new TankAudioTracker(sfx, new RecordingLoopingSfxPlayer(), Profile, clock.Read);
        var projectile = new ProjectilePlacement(Vector3.One, Vector3.UnitX, 7, Vector3.Zero, OwnerEntityId: 2);

        tracker.Resolve(Plan(10, new[] { Self(Vector3.Zero) }, projectile));

        sfx.Paths.Should().NotContain(path => path.EndsWith("/gun.ogg", StringComparison.Ordinal));
        sfx.Paths.Should().NotContain(path => path.EndsWith("/reload.ogg", StringComparison.Ordinal));
    }

    [Fact]
    public void Local_tank_starts_idle_then_movement_enables_drive_layers()
    {
        var clock = new FakeClock();
        var sfx = new RecordingSfxPlayer();
        var loops = new RecordingLoopingSfxPlayer();
        using var tracker = new TankAudioTracker(sfx, loops, Profile, clock.Read);

        tracker.Resolve(Plan(10, new[] { Self(Vector3.Zero) }));
        tracker.Resolve(Plan(11, new[] { Self(Vector3.UnitX) }));

        sfx.Paths.Should().Contain(path => path.EndsWith("/engine-start.ogg", StringComparison.Ordinal));
        sfx.Paths.Should().Contain(path => path.EndsWith("/engine-rev-up.ogg", StringComparison.Ordinal));
        loops.StartedPaths.Should().Contain(path => path.EndsWith("/engine-idle.ogg", StringComparison.Ordinal));
        loops.StartedPaths.Should().Contain(path => path.EndsWith("/engine-high.ogg", StringComparison.Ordinal));
        loops.StartedPaths.Should().Contain(path => path.EndsWith("/tracks.ogg", StringComparison.Ordinal));
        loops.StartedPaths.Should().Contain(path => path.EndsWith("/ground-effect.ogg", StringComparison.Ordinal));
    }

    [Fact]
    public void Throttle_held_against_a_slope_keeps_drive_layers_without_translation()
    {
        var clock = new FakeClock();
        var sfx = new RecordingSfxPlayer();
        var loops = new RecordingLoopingSfxPlayer();
        using var tracker = new TankAudioTracker(sfx, loops, Profile, clock.Read);

        // Engine established at rest, then the player floors the throttle while the hull stays
        // pinned at the same position — uphill against a grade it cannot climb. The drive audio
        // must follow the throttle, not the (zero) translation.
        tracker.Resolve(Plan(10, new[] { Self(Vector3.Zero) }));
        tracker.Resolve(DrivenPlan(11, localThrottle: 1f, Self(Vector3.Zero)));

        sfx.Paths.Should().Contain(path => path.EndsWith("/engine-rev-up.ogg", StringComparison.Ordinal));
        loops.ActivePaths.Should().Contain(path => path.EndsWith("/engine-high.ogg", StringComparison.Ordinal));
        loops.ActivePaths.Should().Contain(path => path.EndsWith("/tracks.ogg", StringComparison.Ordinal));
        loops.ActivePaths.Should().Contain(path => path.EndsWith("/ground-effect.ogg", StringComparison.Ordinal));
    }

    [Fact]
    public void Steering_in_place_without_throttle_engages_the_drive_layers()
    {
        var clock = new FakeClock();
        var sfx = new RecordingSfxPlayer();
        var loops = new RecordingLoopingSfxPlayer();
        using var tracker = new TankAudioTracker(sfx, loops, Profile, clock.Read);

        // Engine established at rest, then the player pivots on the spot: steering hard over with
        // no throttle. The hull spins in place, so it never translates — but the tracks
        // counter-rotate and the engine revs, so the drive audio must engage off the steering.
        tracker.Resolve(Plan(10, new[] { Self(Vector3.Zero) }));
        tracker.Resolve(SteeredPlan(11, localSteering: 1f, Self(Vector3.Zero)));

        sfx.Paths.Should().Contain(path => path.EndsWith("/engine-rev-up.ogg", StringComparison.Ordinal));
        loops.ActivePaths.Should().Contain(path => path.EndsWith("/engine-high.ogg", StringComparison.Ordinal));
        loops.ActivePaths.Should().Contain(path => path.EndsWith("/tracks.ogg", StringComparison.Ordinal));
        loops.ActivePaths.Should().Contain(path => path.EndsWith("/ground-effect.ogg", StringComparison.Ordinal));
    }

    [Fact]
    public void Releasing_throttle_lets_the_high_layer_coast_down_before_dropping_to_idle()
    {
        var clock = new FakeClock();
        var sfx = new RecordingSfxPlayer();
        var loops = new RecordingLoopingSfxPlayer();
        using var tracker = new TankAudioTracker(sfx, loops, Profile, clock.Read);

        tracker.Resolve(Plan(10, new[] { Self(Vector3.Zero) }));
        var driving = DrivenPlan(11, localThrottle: 1f, Self(Vector3.Zero));
        tracker.Resolve(driving);
        PumpFades(tracker, clock, driving, seconds: 0.5); // high layer ramps up to full gain

        var settled = DrivenPlan(12, localThrottle: 0f, Self(Vector3.Zero));
        tracker.Resolve(settled);

        // The instant the throttle is released the high layer is still audible — it coasts down,
        // it does not cut.
        sfx.Paths.Should().Contain(path => path.EndsWith("/engine-rev-down.ogg", StringComparison.Ordinal));
        loops.ActivePaths.Should().Contain(path => path.EndsWith("/engine-high.ogg", StringComparison.Ordinal));

        // After the drive layers have wound down only idle remains.
        PumpFades(tracker, clock, settled, seconds: 1.0);
        loops.ActivePaths.Should().ContainSingle(path => path.EndsWith("/engine-idle.ogg", StringComparison.Ordinal));
    }

    [Fact]
    public void Turret_motion_enables_motor_loop_and_stopping_disables_motion_layers()
    {
        var clock = new FakeClock();
        var sfx = new RecordingSfxPlayer();
        var loops = new RecordingLoopingSfxPlayer();
        using var tracker = new TankAudioTracker(sfx, loops, Profile, clock.Read);

        tracker.Resolve(Plan(10, new[] { Self(Vector3.Zero) }));
        tracker.Resolve(Plan(11, new[] { Self(Vector3.UnitX, turretYaw: 0.2f) }));
        var stopped = Plan(12, new[] { Self(Vector3.UnitX, turretYaw: 0.2f) });
        tracker.Resolve(stopped);

        loops.StartedPaths.Should().Contain(path => path.EndsWith("/turret.ogg", StringComparison.Ordinal));
        sfx.Paths.Should().Contain(path => path.EndsWith("/engine-rev-down.ogg", StringComparison.Ordinal));

        PumpFades(tracker, clock, stopped, seconds: 1.0);
        loops.ActivePaths.Should().ContainSingle(path => path.EndsWith("/engine-idle.ogg", StringComparison.Ordinal));
    }

    [Fact]
    public void Stopped_engine_idle_winds_down_gradually_instead_of_cutting()
    {
        var clock = new FakeClock();
        var sfx = new RecordingSfxPlayer();
        var loops = new RecordingLoopingSfxPlayer();
        using var tracker = new TankAudioTracker(sfx, loops, Profile, clock.Read);

        // Engine idling, with the idle loop brought up to full gain.
        var idling = Plan(10, new[] { Self(Vector3.Zero) });
        tracker.Resolve(idling);
        PumpFades(tracker, clock, idling, seconds: 1.0);
        loops.ActivePaths.Should().Contain(path => path.EndsWith("/engine-idle.ogg", StringComparison.Ordinal));

        // The local tank then leaves the snapshot (destroyed). The stop cough plays once and the
        // idle rumble begins to wind down.
        var gone = Plan(11, Array.Empty<TankPlacement>());
        tracker.Resolve(gone);

        sfx.Paths.Count(path => path.EndsWith("/engine-stop.ogg", StringComparison.Ordinal)).Should().Be(1);

        // Part-way through the wind-down the idle loop is still audible — it is fading, not cut.
        PumpFades(tracker, clock, gone, seconds: 0.4);
        loops.ActivePaths.Should().Contain(path => path.EndsWith("/engine-idle.ogg", StringComparison.Ordinal));

        // Once the wind-down completes the engine has gone fully silent, and the stop cough never
        // retriggered while the snapshot held.
        PumpFades(tracker, clock, gone, seconds: 2.0);
        loops.ActivePaths.Should().BeEmpty();
        sfx.Paths.Count(path => path.EndsWith("/engine-stop.ogg", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public void Stop_disables_every_loop_and_allows_the_next_match_to_start_cleanly()
    {
        var clock = new FakeClock();
        var sfx = new RecordingSfxPlayer();
        var loops = new RecordingLoopingSfxPlayer();
        using var tracker = new TankAudioTracker(sfx, loops, Profile, clock.Read);

        tracker.Resolve(Plan(10, new[] { Self(Vector3.Zero) }));
        tracker.Resolve(Plan(11, new[] { Self(Vector3.UnitX, turretYaw: 0.2f) }));

        tracker.Stop();

        loops.ActivePaths.Should().BeEmpty();

        tracker.Resolve(Plan(10, new[] { Self(Vector3.Zero) }));

        loops.ActivePaths.Should().ContainSingle(path => path.EndsWith("/engine-idle.ogg", StringComparison.Ordinal));
        loops.StartedPaths.Count(path => path.EndsWith("/engine-idle.ogg", StringComparison.Ordinal))
            .Should().Be(2);
    }

    /// <summary>Drives <see cref="TankAudioTracker.Resolve"/> repeatedly on a stationary snapshot
    /// tick, advancing the fake clock in sub-clamp steps so the per-frame fades run to completion.</summary>
    private static void PumpFades(TankAudioTracker tracker, FakeClock clock, NetworkMatchScenePlan plan, double seconds)
    {
        const double stepSeconds = 0.05;
        for (var elapsed = 0.0; elapsed < seconds; elapsed += stepSeconds)
        {
            clock.Advance(TimeSpan.FromSeconds(stepSeconds));
            tracker.Resolve(plan);
        }
    }

    private static readonly TankAudioProfile Profile = new(
        EngineStartPath: "res://audio/test/engine-start.ogg",
        EngineStopPath: "res://audio/test/engine-stop.ogg",
        EngineRevUpPath: "res://audio/test/engine-rev-up.ogg",
        EngineRevDownPath: "res://audio/test/engine-rev-down.ogg",
        EngineIdlePath: "res://audio/test/engine-idle.ogg",
        EngineHighPath: "res://audio/test/engine-high.ogg",
        TracksPath: "res://audio/test/tracks.ogg",
        GroundEffectPath: "res://audio/test/ground-effect.ogg",
        TurretPath: "res://audio/test/turret.ogg",
        GunPath: "res://audio/test/gun.ogg",
        ReloadPath: "res://audio/test/reload.ogg",
        IsDefault: true);

    private static TankPlacement Self(Vector3 position, float turretYaw = 0f) =>
        new(position, 0f, IsSelf: true, KnockedOut: false, EntityId: 1, TurretYawRadians: turretYaw);

    private static NetworkMatchScenePlan Plan(
        long tick,
        IReadOnlyList<TankPlacement> tanks,
        params ProjectilePlacement[] projectiles) =>
        new(Camera, tanks)
        {
            SnapshotTick = tick,
            Projectiles = projectiles,
        };

    private static NetworkMatchScenePlan DrivenPlan(long tick, float localThrottle, params TankPlacement[] tanks) =>
        new(Camera, tanks)
        {
            SnapshotTick = tick,
            LocalThrottle = localThrottle,
        };

    private static NetworkMatchScenePlan SteeredPlan(long tick, float localSteering, params TankPlacement[] tanks) =>
        new(Camera, tanks)
        {
            SnapshotTick = tick,
            LocalSteering = localSteering,
        };

    private sealed class FakeClock
    {
        private TimeSpan _now;

        public TimeSpan Read() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }

    private sealed class RecordingSfxPlayer : ISfxPlayer
    {
        public List<string> Paths { get; } = new();

        public void Play(string vfsPath, float volumeMultiplier = 1f) => Paths.Add(vfsPath);
    }

    private sealed class RecordingLoopingSfxPlayer : ILoopingSfxPlayer
    {
        private readonly List<RecordingLoopingSfxHandle> _handles = new();

        public IEnumerable<string> StartedPaths => _handles.ConvertAll(handle => handle.Path);

        public IEnumerable<string> ActivePaths =>
            _handles.FindAll(handle => handle.IsPlaying).ConvertAll(handle => handle.Path);

        public ILoopingSfxHandle PlayLoop(string vfsPath, float volumeMultiplier = 1f)
        {
            var handle = new RecordingLoopingSfxHandle(vfsPath);
            _handles.Add(handle);
            return handle;
        }
    }

    private sealed class RecordingLoopingSfxHandle : ILoopingSfxHandle
    {
        public RecordingLoopingSfxHandle(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public bool IsPlaying { get; private set; } = true;

        public void SetVolume(float volumeMultiplier)
        {
        }

        public void Stop() => IsPlaying = false;

        public void Dispose() => Stop();
    }
}
