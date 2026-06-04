using System;
using System.Collections.Generic;
using System.Numerics;
using Garupan.Net.Session;
using Garupan.Server.Match.Outcome;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Loop;
using Garupan.Sim.Protocol;
using Garupan.Sim.Snapshot;
using Garupan.Sim.Spawn;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Opus.Foundation;
using Opus.Net.Transport;
using SimWorld = Garupan.Sim.World;

namespace Garupan.Server.Match;

/// <summary>Authoritative match host. Composes an <see cref="INetTransport"/> with a
/// <see cref="ServerSession"/>, a deterministic <see cref="SimWorld"/>, the canonical
/// Phase-0 <see cref="SystemPipeline"/>, and a <see cref="FixedStepLoop"/>. On
/// <see cref="Pump"/> the host drains transport events, advances the sim by the
/// configured tick rate, and (every <see cref="MatchHostOptions.SnapshotIntervalTicks"/>
/// ticks) captures + broadcasts a <see cref="WorldSnapshot"/>. Each tick the host also
/// feeds the roster to a <see cref="MatchOutcomeTracker"/>; once the match is decided it
/// freezes — the sim stops ticking and snapshots stop broadcasting.
/// <para>
/// Peer lifecycle:
/// <list type="number">
/// <item><description>Transport surfaces a <c>Connected</c> → host allocates the next
/// spawn slot, draws a fresh <see cref="NetworkId"/> from a monotonic counter, calls
/// <see cref="TankSpawner"/> with <see cref="TankControl.None"/> (the server is the
/// controller; the input adapter never runs on a peer-controlled tank) and that network
/// id, sends Welcome carrying it.</description></item>
/// <item><description>Transport surfaces a <c>Received</c> SVOI → host routes the
/// frame's owner to the matching <see cref="ConnectedPlayer"/> via the connection map,
/// writes a fresh <see cref="PendingInput"/> onto the entity. <see cref="ApplyInputsSystem"/>
/// commits it on the next tick.</description></item>
/// <item><description>Transport surfaces a <c>Disconnected</c> → host destroys the
/// entity and drops the map row. Subsequent snapshots no longer include the row.</description></item>
/// </list>
/// </para>
/// <para>
/// The host does NOT own its transport — Phase 32 callers pass a
/// <see cref="LoopbackTransport"/> in unit tests; a future UDP transport drops in the
/// same way without host-side changes. <see cref="Dispose"/> releases the world + the
/// session, not the transport.
/// </para>
/// <para>
/// This orchestrator intentionally stays above 300 lines: it owns one match's ordered network,
/// world, outcome, snapshot, and peer lifecycle. Leaf policies remain in focused collaborators.
/// </para>
/// </summary>
public sealed class MatchHost : IDisposable
{
    private readonly INetTransport _transport;
    private readonly MatchHostOptions _options;
    private readonly ILogger<MatchHost> _logger;
    private readonly ServerSession _session;
    private readonly SimWorld _world;
    private readonly SystemPipeline _pipeline;
    private readonly FixedStepLoop _tickLoop;
    private readonly MatchRoster _roster = new();
    private readonly MatchOutcomeTracker _outcomeTracker;
    private readonly List<MatchParticipant> _participantScratch = new();
    private GameTime _time;
    private int _ticksSinceLastSnapshot;
    private int _snapshotsBroadcast;
    private int _lastBroadcastDeliveredCount;
    private int _matchesPlayed;

    // -1 = not armed (no decided match yet, or auto-reset disabled). Set on the deciding
    // tick to PostMatchHoldTicks; counts down each frozen tick; on reaching 0 the host
    // calls ResetMatch and starts the next round.
    private int _postMatchTicksRemaining = -1;
    private bool _disposed;

    public MatchHost(INetTransport transport, MatchHostOptions options, ILogger<MatchHost>? logger = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<MatchHost>.Instance;

        _session = new ServerSession(_transport, NullLogger<ServerSession>.Instance);
        _world = SimWorld.Create();
        MapObstacleSpawner.Spawn(_world, _options.MapObstacles);
        MapPropSpawner.Spawn(_world, _options.MapProps);
        _pipeline = MatchPipelineFactory.Build(
            _options.RespawnDelayTicks,
            _options.SpawnInvulnerabilityTicks,
            _options.TerrainHeightSampler);
        _tickLoop = new FixedStepLoop(_options.TickRateHz);
        _time = GameTime.AtRate(_options.TickRateHz);
        _tickLoop.OnTick = OnTick;
        _outcomeTracker = new MatchOutcomeTracker(_options.OutcomeRule);

        _session.PeerConnected += OnPeerConnected;
        _session.PeerDisconnected += OnPeerDisconnected;
        _session.ClientInputReceived += OnClientInputReceived;
    }

    /// <summary>The authoritative world the host ticks. Exposed for diagnostics + tests;
    /// runtime callers should drive everything via <see cref="Pump"/>.</summary>
    public SimWorld World => _world;

    /// <summary>Current authoritative tick. Increments on every <see cref="OnTick"/>.</summary>
    public Tick CurrentTick => _time.Tick;

    /// <summary>How many peers are currently in the match.</summary>
    public int PlayerCount => _roster.Count;

    /// <summary>Number of snapshots broadcast since construction. Useful for tests that
    /// assert "at least N snapshots fanned out".</summary>
    public int SnapshotsBroadcast => _snapshotsBroadcast;

    /// <summary>Number of peers the most recent snapshot reached — equal to
    /// <see cref="PlayerCount"/> when every link was healthy at broadcast time.</summary>
    public int LastBroadcastDeliveredCount => _lastBroadcastDeliveredCount;

    /// <summary>The match verdict — <see cref="MatchOutcome.InProgress"/> while the match
    /// is being contested, then the latched winner / draw. Once decided the host freezes:
    /// the sim no longer ticks and snapshots no longer broadcast.</summary>
    public MatchOutcome Outcome => _outcomeTracker.Current;

    /// <summary>How many matches the host has played since construction. Increments on
    /// every <see cref="ResetMatch"/> — the first decided match leaves this at 1.</summary>
    public int MatchesPlayed => _matchesPlayed;

    /// <summary>One driver call per real frame. Pumps the network layer (events fire
    /// synchronously inside this call), then advances the deterministic sim by the
    /// configured tick rate. Snapshot broadcasts ride on the per-tick path.</summary>
    public void Pump(double frameDeltaSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _session.Pump();
        _tickLoop.Pump(frameDeltaSeconds);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.PeerConnected -= OnPeerConnected;
        _session.PeerDisconnected -= OnPeerDisconnected;
        _session.ClientInputReceived -= OnClientInputReceived;
        _session.Dispose();
        _world.Dispose();
        _roster.Clear();
    }

    /// <summary>Recycles the host for the next match. Destroys every player entity in
    /// the authoritative world, resets the outcome tracker, and re-spawns each connected
    /// peer with a fresh <see cref="NetworkId"/> + a new <see cref="WelcomeFrame"/>. The
    /// <see cref="ConnectionId"/>s stay stable — peers do NOT see a disconnect, just a
    /// new Welcome that the client treats as a match boundary. Called automatically by
    /// the post-match hold counter (<see cref="MatchHostOptions.PostMatchHoldTicks"/>);
    /// also callable directly by operations tooling.</summary>
    public void ResetMatch()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Snapshot the current peer list — the roster will be re-keyed by the re-spawn pass.
        var peersToRespawn = new List<ConnectionId>(_roster.Count);
        foreach (var player in _roster.Players)
        {
            peersToRespawn.Add(player.Connection);
            if (_world.IsAlive(player.Entity))
            {
                _world.Destroy(player.Entity);
            }
        }

        _roster.Clear();
        _outcomeTracker.Reset();
        _postMatchTicksRemaining = -1;
        MapPropRoundReset.RestoreStanding(_world);

        foreach (var peer in peersToRespawn)
        {
            SpawnPlayerForPeer(peer);
        }

        _matchesPlayed++;
        _logger.LogInformation(
            "Match reset on tick {Tick}: next round = match #{Count}, {Players} players seated.",
            _time.Tick.Value,
            _matchesPlayed + 1,
            _roster.Count);
    }

    private void OnTick(GameTime time)
    {
        _time = time;

        // A decided match is frozen: the authoritative sim no longer advances and no
        // further snapshots fan out. The tick counter still moves so the wall-clock
        // driver loop stays well-behaved. The match-over verdict was already broadcast
        // to clients on the deciding tick by EvaluateOutcome. Once the configured
        // post-match hold ticks elapse, ResetMatch recycles the host for the next round.
        if (_outcomeTracker.Current.IsDecided)
        {
            AdvancePostMatchHold();
            return;
        }

        _pipeline.Tick(_world, time, SimSeed.Zero);
        EvaluateOutcome();

        _ticksSinceLastSnapshot++;
        if (_ticksSinceLastSnapshot >= _options.SnapshotIntervalTicks)
        {
            _ticksSinceLastSnapshot = 0;
            BroadcastSnapshot();
        }
    }

    /// <summary>Counts down <see cref="_postMatchTicksRemaining"/> while the match is
    /// frozen post-verdict. When the timer hits zero (and was armed in the first place)
    /// the host recycles via <see cref="ResetMatch"/>. A negative counter means
    /// auto-reset is disabled — the match stays frozen indefinitely until the host
    /// process exits, the legacy behaviour.</summary>
    private void AdvancePostMatchHold()
    {
        if (_postMatchTicksRemaining < 0)
        {
            return;
        }

        if (_postMatchTicksRemaining > 0)
        {
            _postMatchTicksRemaining--;
            return;
        }

        ResetMatch();
    }

    /// <summary>Rebuilds the participant roster from the authoritative world and feeds it
    /// to the outcome tracker. Runs once per tick after the pipeline; the scratch list is
    /// reused so the per-tick path stays allocation-free.</summary>
    private void EvaluateOutcome()
    {
        _participantScratch.Clear();
        foreach (var player in _roster.Players)
        {
            if (!_world.IsAlive(player.Entity))
            {
                continue;
            }

            _participantScratch.Add(new MatchParticipant(
                player.NetworkId,
                player.Team,
                MatchHostKnockoutEvaluator.IsFinallyKnockedOut(_world, player.Entity)));
        }

        var wasDecided = _outcomeTracker.Current.IsDecided;
        _outcomeTracker.Update(_participantScratch);
        if (wasDecided || !_outcomeTracker.Current.IsDecided)
        {
            return;
        }

        var outcome = _outcomeTracker.Current;
        _logger.LogInformation(
            "Match decided on tick {Tick}: kind={Kind}, winner_network_id={WinnerNetworkId}, winner_team={WinnerTeam}.",
            _time.Tick.Value,
            outcome.Kind,
            outcome.WinnerNetworkId,
            outcome.WinnerTeam);
        _session.BroadcastMatchOver(MatchWireProjections.ToMatchOverFrame(outcome));
        _postMatchTicksRemaining = _options.PostMatchHoldTicks > 0
            ? _options.PostMatchHoldTicks
            : -1;
    }

    private void BroadcastSnapshot()
    {
        var snapshot = SnapshotCapture.Capture(_world, _time.Tick);
        _lastBroadcastDeliveredCount = _session.BroadcastSnapshot(snapshot);
        _snapshotsBroadcast++;
    }

    private void OnPeerConnected(ConnectionId peer)
    {
        var player = SpawnPlayerForPeer(peer);

        _logger.LogInformation(
            "Peer {Connection} joined match: network_id={NetworkId}, team={Team}, spawn_index={Spawn}, total_players={Count}.",
            peer,
            player.NetworkId,
            player.Team,
            player.SpawnIndex,
            _roster.Count);
    }

    /// <summary>Spawns a tank for <paramref name="peer"/> and records the resulting
    /// <see cref="ConnectedPlayer"/> in the roster. Shared by initial-connect and
    /// <see cref="ResetMatch"/>; the second path keeps the same <see cref="ConnectionId"/>
    /// but draws a fresh <see cref="NetworkId"/> + sends a new <see cref="WelcomeFrame"/>
    /// (the canonical "match boundary" signal the client clears its match-over banner on).
    /// Team is resolved first so the spawn anchor + yaw read the per-team values — a
    /// Tactical 5v5 round opens with the two sides on opposite anchors, gun barrels at
    /// each other, not in one overlapping pile at <see cref="MatchHostOptions.SpawnAnchor"/>.</summary>
    private ConnectedPlayer SpawnPlayerForPeer(ConnectionId peer)
    {
        var (playerSchool, opponentSchool) = MatchHostSpawnPlanner.CountTeams(_roster.Players);
        var team = TeamAssignment.NextTeam(_options.OutcomeRule, _options.PlayerTeam, playerSchool, opponentSchool);
        var isCommander = TeamAssignment.IsCommander(_options.OutcomeRule, team, playerSchool, opponentSchool);
        var teamSlot = team == Team.OpponentSchool ? opponentSchool : playerSchool;
        var spawnPos = MatchHostSpawnPlanner.ComputeSpawn(_options, team, teamSlot);
        var spawnYaw = MatchHostSpawnPlanner.ComputeSpawnYaw(team);
        var spawnIndex = _roster.DrawSpawnIndex();
        var networkId = _roster.DrawNetworkId();
        var entity = TankSpawner.Spawn(
            _world,
            _options.PlayerSpec,
            spawnPos,
            yawRadians: spawnYaw,
            team,
            TankControl.None,
            networkId: networkId,
            respawnLives: _options.RespawnsPerPeer);

        var player = new ConnectedPlayer(peer, networkId.Value, entity, spawnIndex, team);
        _roster.Seat(player);
        _session.SendWelcome(peer, new WelcomeFrame(
            NetworkId: networkId.Value,
            TeamId: (byte)team,
            ModeKind: MatchWireProjections.ToWelcomeModeKind(_options.OutcomeRule),
            RespawnsConfigured: _options.RespawnsPerPeer,
            IsCommander: isCommander));
        return player;
    }

    private void OnPeerDisconnected(ConnectionId peer)
    {
        if (!_roster.Remove(peer, out var player))
        {
            return;
        }

        if (_world.IsAlive(player.Entity))
        {
            _world.Destroy(player.Entity);
        }

        _logger.LogInformation(
            "Peer {Peer} left match: network_id={NetworkId}, remaining_players={Count}.",
            peer,
            player.NetworkId,
            _roster.Count);
    }

    private void OnClientInputReceived(ConnectionId peer, ClientInputFrame frame)
    {
        if (!_roster.TryGet(peer, out var player))
        {
            _logger.LogWarning("Dropped input from unknown peer {Peer}.", peer);
            return;
        }

        if (!_world.IsAlive(player.Entity))
        {
            _logger.LogWarning(
                "Dropped input from {Peer}: entity {Entity} is no longer alive.",
                peer,
                player.Entity);
            return;
        }

        _world.AddOrSet(player.Entity, new PendingInput
        {
            Tick = frame.Tick,
            Throttle = frame.Throttle,
            Steering = frame.Steering,
            TurretYawRadians = frame.TurretYawRadians,
            BarrelPitchRadians = frame.BarrelPitchRadians,
            Flags = frame.Flags,
        });
    }
}
