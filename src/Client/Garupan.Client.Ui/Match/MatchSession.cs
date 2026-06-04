using System;
using Garupan.Content;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Loop;
using Garupan.Sim.Spawn;
using Opus.Foundation;
using SimWorld = Garupan.Sim.World;

namespace Garupan.Client.Ui.Match;

/// <summary>
/// Owns one match's worth of ECS state: the World, the system pipeline, the player
/// entity, and the win/lose tracker. Created by <see cref="Screens.Match.MatchScreen"/>
/// on enter, disposed on exit. Single-player Phase-0 only — no networking, no multi-team.
///
/// Pipeline composition lives in <see cref="MatchPipelineFactory"/>; victory / defeat
/// detection in <see cref="MatchOutcomeTracker"/>. This file is lifecycle only.
/// </summary>
public sealed class MatchSession : IDisposable
{
    private const float DefaultMatchHalfExtentMeters = 80f;

    private readonly SimWorld _world;
    private readonly SystemPipeline _pipeline;
    private readonly MatchOutcomeTracker _tracker;
    private GameTime _time;
    private bool _disposed;

    private MatchSession(SimWorld world, SystemPipeline pipeline, MatchOutcomeTracker tracker, EntityHandle player)
    {
        _world = world;
        _pipeline = pipeline;
        _tracker = tracker;
        Player = player;
        _time = GameTime.AtRate(SimulationConstants.TicksPerSecond);
    }

    public SimWorld World => _world;

    public EntityHandle Player { get; }

    public float HalfExtentMeters { get; private set; } = DefaultMatchHalfExtentMeters;

    public MatchOutcome Outcome => _tracker.Outcome;

    public int AlivePlayers => _tracker.AlivePlayers;

    public int AliveOpponents => _tracker.AliveOpponents;

    public static MatchSession Create(MatchSetup setup)
    {
        Ensure.NotNull(setup);

        var world = SimWorld.Create();
        var pipeline = MatchPipelineFactory.BuildPhase0();

        var player = TankSpawner.Spawn(
            world,
            setup.PlayerTank,
            setup.PlayerSpawn,
            setup.PlayerYaw,
            Team.PlayerSchool,
            TankControl.Player);

        foreach (var opponent in setup.Opponents)
        {
            TankSpawner.Spawn(
                world,
                opponent.Spec,
                opponent.Position,
                opponent.Yaw,
                Team.OpponentSchool,
                TankControl.AiBot);
        }

        var tracker = new MatchOutcomeTracker();
        tracker.Seed(players: 1, opponents: setup.Opponents.Count);

        return new MatchSession(world, pipeline, tracker, player)
        {
            HalfExtentMeters = setup.HalfExtentMeters,
        };
    }

    public void Tick()
    {
        if (_tracker.Outcome != MatchOutcome.InProgress)
        {
            return;
        }

        _pipeline.Tick(_world, _time, SimSeed.Zero);
        _time = _time.Advance();
        _tracker.Update(_world);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _world.Dispose();
        _disposed = true;
    }
}

public enum MatchOutcome
{
    InProgress,
    Victory,
    Defeat,
}

/// <summary>One opponent's spawn parameters.</summary>
public sealed record OpponentSetup(TankSpec Spec, System.Numerics.Vector2 Position, float Yaw);

/// <summary>What goes into a match — passed to <see cref="MatchSession.Create"/>.</summary>
public sealed record MatchSetup(
    TankSpec PlayerTank,
    System.Numerics.Vector2 PlayerSpawn,
    float PlayerYaw,
    System.Collections.Generic.IReadOnlyList<OpponentSetup> Opponents,
    float HalfExtentMeters = 80f);
