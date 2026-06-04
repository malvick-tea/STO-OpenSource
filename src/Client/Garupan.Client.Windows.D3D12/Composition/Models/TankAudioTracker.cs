using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Garupan.Client.Ui.Match.Network;
using Garupan.Content;
using Opus.Engine.Audio;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>Maps authoritative local-tank motion and new projectile ids onto a small
/// provisional tank audio profile. Snapshot repetition is harmless.</summary>
/// <remarks>
/// The looping channels (engine, tracks, turret) never snap on or off: each ramps toward a target
/// gain through a <see cref="FadingSfxLoop"/>, so the engine winds down to silence over time when
/// the tank stops instead of cutting abruptly. Gameplay state (which loops are wanted, gun shots)
/// is resolved once per new snapshot tick; the fades themselves advance every frame off a
/// wall-clock seam, so a wind-down completes smoothly even while snapshots are paused.
/// </remarks>
internal sealed class TankAudioTracker : IDisposable
{
    private const float MotionThresholdSquaredMeters = 0.0001f;
    private const float TurretMotionThresholdRadians = 0.001f;

    /// <summary>Drive-command magnitude (throttle or steering) above which the local tank is
    /// "under power" and the drive audio engages regardless of hull translation. Mirrors the
    /// solver's off-throttle deadband so a stationary hull still drives its loops when it is
    /// either flooring the throttle (pinned on a slope) or pivoting in place (steering only —
    /// the tracks counter-rotate and the engine revs though the hull covers no ground).</summary>
    private const float DriveInputDeadband = 0.05f;

    /// <summary>Upper bound on a single fade step so a stalled frame (or the first observation)
    /// nudges the gains smoothly instead of jumping the engine straight to its target.</summary>
    private const float MaxFadeStepSeconds = 0.1f;

    // Loop mix levels. Idle ducks under the high-engine layer once the tank is under drive.
    private const float IdleVolume = 0.62f;
    private const float IdleDuckedVolume = 0.34f;
    private const float EngineHighVolume = 0.78f;
    private const float TracksVolume = 0.72f;
    private const float GroundEffectVolume = 0.44f;
    private const float TurretVolume = 0.56f;

    // Spin-up is brisk; wind-down lingers so the powertrain coasts to silence. Idle owns the
    // slowest tail — that long fade is the audible "engine stopping" the rest of the mix sits on —
    // while the turret motor is the snappiest because a hand-cranked traverse stops near-instantly.
    private static readonly FadeProfile IdleFade = new(FadeInSeconds: 0.45f, FadeOutSeconds: 1.6f);
    private static readonly FadeProfile DriveFade = new(FadeInSeconds: 0.3f, FadeOutSeconds: 0.7f);
    private static readonly FadeProfile TurretFade = new(FadeInSeconds: 0.12f, FadeOutSeconds: 0.3f);

    private readonly ISfxPlayer _sfx;
    private readonly TankAudioProfile _profile;
    private readonly Func<TimeSpan> _clock;
    private readonly HashSet<int> _activeProjectileIds = new();

    private readonly FadingSfxLoop _engineIdle;
    private readonly FadingSfxLoop _engineHigh;
    private readonly FadingSfxLoop _tracks;
    private readonly FadingSfxLoop _groundEffect;
    private readonly FadingSfxLoop _turret;

    private TankAudioPose? _previousPose;
    private TimeSpan? _lastClock;
    private long? _lastSnapshotTick;
    private bool _engineRunning;
    private bool _underDrive;
    private bool _disposed;

    public TankAudioTracker(ISfxPlayer sfx, ILoopingSfxPlayer loops, TankAudioProfile profile, Func<TimeSpan>? clock = null)
    {
        _sfx = sfx ?? throw new ArgumentNullException(nameof(sfx));
        ArgumentNullException.ThrowIfNull(loops);
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _clock = clock ?? CreateStopwatchClock();
        _engineIdle = new FadingSfxLoop(loops, profile.EngineIdlePath, IdleFade);
        _engineHigh = new FadingSfxLoop(loops, profile.EngineHighPath, DriveFade);
        _tracks = new FadingSfxLoop(loops, profile.TracksPath, DriveFade);
        _groundEffect = new FadingSfxLoop(loops, profile.GroundEffectPath, DriveFade);
        _turret = new FadingSfxLoop(loops, profile.TurretPath, TurretFade);
    }

    public void Resolve(NetworkMatchScenePlan plan)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(plan);

        // Gameplay state re-resolves only on a new snapshot tick (setting each loop's target gain);
        // the fades then advance every frame off the wall clock, so a wind-down stays smooth while
        // the snapshot is paused AND the targets set this frame take effect this same frame.
        var deltaSeconds = StepClock();
        if (_lastSnapshotTick != plan.SnapshotTick)
        {
            ResolveSnapshot(plan);
        }

        AdvanceFades(deltaSeconds);
    }

    private void ResolveSnapshot(NetworkMatchScenePlan plan)
    {
        if (_lastSnapshotTick is { } previousTick && plan.SnapshotTick < previousTick)
        {
            SilenceTank();
            _activeProjectileIds.Clear();
        }

        _lastSnapshotTick = plan.SnapshotTick;

        var self = FindSelf(plan);
        ResolveShots(plan, self?.EntityId ?? 0);
        if (self is null || self.Value.KnockedOut)
        {
            WindDownTank();
            return;
        }

        var currentPose = new TankAudioPose(self.Value.Position, self.Value.TurretYawRadians);
        if (!_engineRunning)
        {
            _sfx.Play(_profile.EngineStartPath);
            _engineRunning = true;
        }

        // Engine + track audio follow drive EFFORT, not hull translation: a tank flooring the
        // throttle against an unclimbable slope — OR pivoting in place on the spot (steering with
        // no throttle, tracks counter-rotating) — still roars and grinds its tracks though it
        // covers no ground. So "under drive" is translating OR commanding throttle OR steering;
        // the rev edge and the loop gates key off it, and the player's own input is the signal.
        var translating = _previousPose is { } pose &&
            Vector3.DistanceSquared(pose.Position, currentPose.Position) > MotionThresholdSquaredMeters;
        var underPower = MathF.Abs(plan.LocalThrottle) > DriveInputDeadband
            || MathF.Abs(plan.LocalSteering) > DriveInputDeadband;
        var underDrive = translating || underPower;
        var turretMoving = _previousPose is { } turretPose &&
            MathF.Abs(WrapRadians(currentPose.TurretYawRadians - turretPose.TurretYawRadians)) >
            TurretMotionThresholdRadians;

        if (underDrive != _underDrive)
        {
            _sfx.Play(underDrive ? _profile.EngineRevUpPath : _profile.EngineRevDownPath);
        }

        _engineIdle.SetTarget(underDrive ? IdleDuckedVolume : IdleVolume);
        _engineHigh.SetTarget(underDrive ? EngineHighVolume : 0f);
        _tracks.SetTarget(underDrive ? TracksVolume : 0f);
        _groundEffect.SetTarget(underDrive ? GroundEffectVolume : 0f);
        _turret.SetTarget(turretMoving ? TurretVolume : 0f);

        _underDrive = underDrive;
        _previousPose = currentPose;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Reset();
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Reset();
    }

    private void ResolveShots(NetworkMatchScenePlan plan, int localTankId)
    {
        var currentIds = new HashSet<int>();
        for (var i = 0; i < plan.Projectiles.Count; i++)
        {
            var id = plan.Projectiles[i].Id;
            currentIds.Add(id);
            if (_activeProjectileIds.Add(id) && plan.Projectiles[i].OwnerEntityId == localTankId && localTankId != 0)
            {
                _sfx.Play(_profile.GunPath);
                _sfx.Play(_profile.ReloadPath);
            }
        }

        _activeProjectileIds.IntersectWith(currentIds);
    }

    /// <summary>Begins an engine wind-down: every loop ramps to silence and the engine-stop
    /// cough plays once. The fade itself plays out over subsequent <see cref="AdvanceFades"/> calls,
    /// so a knocked-out or departed tank coasts to silence instead of cutting.</summary>
    private void WindDownTank()
    {
        _engineIdle.SetTarget(0f);
        _engineHigh.SetTarget(0f);
        _tracks.SetTarget(0f);
        _groundEffect.SetTarget(0f);
        _turret.SetTarget(0f);
        _previousPose = null;
        _underDrive = false;

        if (_engineRunning)
        {
            _sfx.Play(_profile.EngineStopPath);
        }

        _engineRunning = false;
    }

    /// <summary>Cuts every loop without a fade — for match reuse, teardown, or disposal, where no
    /// further frames will run to carry a wind-down to completion.</summary>
    private void SilenceTank()
    {
        _engineIdle.StopImmediately();
        _engineHigh.StopImmediately();
        _tracks.StopImmediately();
        _groundEffect.StopImmediately();
        _turret.StopImmediately();
        _previousPose = null;
        _underDrive = false;
        _engineRunning = false;
    }

    private void Reset()
    {
        SilenceTank();
        _activeProjectileIds.Clear();
        _lastSnapshotTick = null;
        _lastClock = null;
    }

    /// <summary>Reads the wall clock and returns the clamped seconds elapsed since the previous
    /// frame. The clamp keeps a stalled frame (or the first observation) from jumping a fade.</summary>
    private float StepClock()
    {
        var now = _clock();
        var deltaSeconds = _lastClock is { } previous ? (float)(now - previous).TotalSeconds : 0f;
        _lastClock = now;
        return Math.Clamp(deltaSeconds, 0f, MaxFadeStepSeconds);
    }

    private void AdvanceFades(float deltaSeconds)
    {
        _engineIdle.Advance(deltaSeconds);
        _engineHigh.Advance(deltaSeconds);
        _tracks.Advance(deltaSeconds);
        _groundEffect.Advance(deltaSeconds);
        _turret.Advance(deltaSeconds);
    }

    private static Func<TimeSpan> CreateStopwatchClock()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed;
    }

    private static TankPlacement? FindSelf(NetworkMatchScenePlan plan)
    {
        for (var i = 0; i < plan.Tanks.Count; i++)
        {
            if (plan.Tanks[i].IsSelf)
            {
                return plan.Tanks[i];
            }
        }

        return null;
    }

    private static float WrapRadians(float radians)
    {
        while (radians > MathF.PI)
        {
            radians -= MathF.Tau;
        }

        while (radians < -MathF.PI)
        {
            radians += MathF.Tau;
        }

        return radians;
    }

    private readonly record struct TankAudioPose(Vector3 Position, float TurretYawRadians);
}
