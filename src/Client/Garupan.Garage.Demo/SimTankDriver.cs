using System;
using System.Collections.Generic;
using System.Numerics;
using Garupan.Content;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Snapshot;
using Garupan.Sim.Spawn;
using Garupan.Sim.Systems;

namespace Garupan.Garage.Demo;

/// <summary>
/// M4.a-i Sim hookup for the standalone Garage demo. Owns a <see cref="World"/> with one
/// player-controlled tank plus zero or more AI-controlled opponents, runs the full Phase-0
/// battle pipeline (Apply inputs → AI → drive → aim → reload → fire → integrate → hit
/// resolve → lifetime → cleanup) at fixed step via <see cref="FixedStepLoop"/>, and
/// derives a high-level <see cref="MatchOutcome"/> each tick from the player + opponent
/// <see cref="KnockedOut"/> flags. The host pushes WASD + Space intent via
/// <see cref="SubmitInput"/>; <see cref="Tick"/> pumps the sim and captures the latest
/// poses, projectile list, and outcome for the renderer.
/// </summary>
public sealed class SimTankDriver : IDisposable
{
    private readonly World _world;
    private readonly SystemPipeline _pipeline;
    private readonly FixedStepLoop _loop;
    private readonly EntityHandle _player;
    private readonly List<EntityHandle> _opponents = new();
    private readonly List<long?> _opponentKoTicks = new();
    private readonly List<OpponentSchool> _opponentSchools = new();

    private float _throttle;
    private float _steering;
    private bool _fireRequested;
    private long? _playerKoTick;

    public SimTankDriver(TankSpec spec, Vector2 spawnPosition, float spawnYawRadians)
    {
        ArgumentNullException.ThrowIfNull(spec);

        _world = World.Create();
        _player = TankSpawner.Spawn(_world, spec, spawnPosition, spawnYawRadians, Team.PlayerSchool, TankControl.Player);
        PlayerSchool = spec.School;

        _pipeline = new SystemPipeline(new ISystem[]
        {
            new ApplyInputsSystem(),
            new AiBotSystem(),
            new HullDriveSystem(),
            new TurretAimSystem(),
            new ReloadTickSystem(),
            new ProjectileIntegrateSystem(),
            new GunFireSystem(),
            new ProjectileHitResolveSystem(),
            new LifetimeDecaySystem(),
            new CleanupDeadSystem(),
        });
        _loop = new FixedStepLoop();
        _loop.OnTick = time => _pipeline.Tick(_world, time, SimSeed.Zero);

        PlayerPositionXY = spawnPosition;
        PlayerYawRadians = spawnYawRadians;
    }

    /// <summary>Canon school of the player tank, captured at spawn time from
    /// <see cref="TankSpec.School"/>. Hosts use this to look up the player's camo tint.</summary>
    public OpponentSchool PlayerSchool { get; }

    /// <summary>Latest player-tank position, in Sim top-down XY metres.</summary>
    public Vector2 PlayerPositionXY { get; private set; }

    /// <summary>Latest player-tank hull yaw in radians, CCW positive, 0 = facing +X.</summary>
    public float PlayerYawRadians { get; private set; }

    /// <summary>Number of fixed-step ticks the loop has fired since construction.</summary>
    public long CurrentTick => _loop.CurrentTick.Value;

    public int OpponentCount => _opponents.Count;

    public bool IsPlayerKnockedOut => _world.Has<KnockedOut>(_player);

    /// <summary>Most recent <see cref="WorldSnapshot"/> captured after the latest
    /// <see cref="Tick"/>. Null until the first tick has fired.</summary>
    public WorldSnapshot? LatestSnapshot { get; private set; }

    /// <summary>Current high-level state of the match — derived from the player + opponent
    /// <see cref="KnockedOut"/> flags. Recomputed every <see cref="Tick"/>.</summary>
    public MatchOutcome Outcome { get; private set; } = MatchOutcome.InProgress;

    /// <summary>Spawns an AI-controlled enemy on <see cref="Team.OpponentSchool"/> with the
    /// default <see cref="BotBrain"/> (60 m engage range). Returns the slot index callers
    /// pass to <see cref="GetOpponentPosition"/> / <see cref="GetOpponentYaw"/> /
    /// <see cref="IsOpponentKnockedOut"/>.</summary>
    public int SpawnOpponent(TankSpec spec, Vector2 position, float yawRadians)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var handle = TankSpawner.Spawn(_world, spec, position, yawRadians, Team.OpponentSchool, TankControl.AiBot);
        _opponents.Add(handle);
        _opponentKoTicks.Add(null);
        _opponentSchools.Add(spec.School);
        return _opponents.Count - 1;
    }

    /// <summary>Canon school of the opponent at <paramref name="index"/>, captured at
    /// spawn time from <see cref="TankSpec.School"/>. Hosts use this to look up per-school
    /// camo tint without keeping a parallel array.</summary>
    public OpponentSchool GetOpponentSchool(int index) => _opponentSchools[index];

    /// <summary>Sim tick at which the player tank first appeared <see cref="KnockedOut"/>,
    /// or null if the player is still alive. Hosts use the tick delta to drive a smoothed
    /// KO-tilt animation — `(CurrentTick - tick) / 60` gives seconds since the hit.</summary>
    public long? PlayerKnockedOutAtTick => _playerKoTick;

    /// <summary>Sim tick at which the opponent at <paramref name="index"/> first appeared
    /// <see cref="KnockedOut"/>, or null if still alive.</summary>
    public long? GetOpponentKnockedOutAtTick(int index) => _opponentKoTicks[index];

    public Vector2 GetOpponentPosition(int index) =>
        _world.Get<Transform>(_opponents[index]).Position;

    public float GetOpponentYaw(int index) =>
        _world.Get<Transform>(_opponents[index]).YawRadians;

    public bool IsOpponentKnockedOut(int index) =>
        _world.Has<KnockedOut>(_opponents[index]);

    /// <summary>Records the player's current intent. <paramref name="fire"/> is a per-tick
    /// flag — <see cref="GunFireSystem"/> silently drops the intent while reloading.</summary>
    public void SubmitInput(float throttle, float steering, bool fire = false)
    {
        _throttle = throttle;
        _steering = steering;
        _fireRequested = fire;
    }

    /// <summary>Pumps the fixed-step loop over <paramref name="deltaSeconds"/> of real
    /// time, captures the player pose + world snapshot + match outcome for the renderer.</summary>
    public void Tick(double deltaSeconds)
    {
        StagePendingInput();
        _loop.Pump(deltaSeconds);
        CapturePlayerPose();
        LatestSnapshot = SnapshotCapture.Capture(_world, _loop.CurrentTick);
        TrackKnockoutTicks();
        Outcome = ComputeOutcome();
    }

    private void TrackKnockoutTicks()
    {
        var currentTick = _loop.CurrentTick.Value;
        if (_playerKoTick is null && IsPlayerKnockedOut)
        {
            _playerKoTick = currentTick;
        }

        for (var i = 0; i < _opponents.Count; i++)
        {
            if (_opponentKoTicks[i] is null && _world.Has<KnockedOut>(_opponents[i]))
            {
                _opponentKoTicks[i] = currentTick;
            }
        }
    }

    public void Dispose()
    {
        _world.Dispose();
    }

    private MatchOutcome ComputeOutcome()
    {
        if (IsPlayerKnockedOut)
        {
            return MatchOutcome.Defeat;
        }

        if (_opponents.Count == 0)
        {
            return MatchOutcome.InProgress;
        }

        for (var i = 0; i < _opponents.Count; i++)
        {
            if (!_world.Has<KnockedOut>(_opponents[i]))
            {
                return MatchOutcome.InProgress;
            }
        }

        return MatchOutcome.Victory;
    }

    private void StagePendingInput()
    {
        var pending = new PendingInput
        {
            Tick = (ulong)_loop.CurrentTick.Value,
            Throttle = _throttle,
            Steering = _steering,
            TurretYawRadians = PlayerYawRadians,
            Flags = _fireRequested ? InputFlags.Fire : InputFlags.None,
        };

        if (_world.Has<PendingInput>(_player))
        {
            _world.Set(_player, pending);
        }
        else
        {
            _world.Add(_player, pending);
        }
    }

    private void CapturePlayerPose()
    {
        ref var tf = ref _world.Get<Transform>(_player);
        PlayerPositionXY = tf.Position;
        PlayerYawRadians = tf.YawRadians;
    }
}
