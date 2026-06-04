using System;
using System.Diagnostics;
using Garupan.Net.Session;
using Garupan.Sim.Protocol;
using Garupan.Sim.Snapshot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Opus.Net.Transport;

namespace Garupan.Client.Ui.Match.Network;

/// <summary>
/// Client-side composition of one network match. Owns a caller-supplied
/// <see cref="INetTransport"/> + a <see cref="ClientSession"/> wrapping it; tracks the
/// connect/play/drop lifecycle in a single <see cref="State"/> field; exposes the latest
/// <see cref="WorldSnapshot"/> + local <see cref="LocalNetworkId"/> the renderer needs;
/// forwards encoded <see cref="ClientInputFrame"/> sends through the session.
/// </summary>
/// <remarks>
/// <para>
/// Single responsibility — bridge transport+session into a UI-friendly surface. No
/// rendering, no input reading; <see cref="NetworkMatchScreen"/> owns those concerns. The
/// transport ctor injection lets tests drive the client through a loopback hub instead
/// of a real UDP socket; runtime wiring at the lobby builds the transport with
/// <see cref="NetworkMatchClientFactory"/>.
/// </para>
/// <para>
/// <see cref="Pump"/> runs from the game tick — drains transport events, dispatches them
/// to events, never touches the transport's receive thread. The latest snapshot is held
/// by-value (record struct) so a tick that observes the field gets a coherent frame
/// even if the network thread is mid-publish.
/// </para>
/// </remarks>
public sealed class NetworkMatchClient : IDisposable
{
    /// <summary>How long the client waits for a WelcomeAck before giving up the connect
    /// attempt to <see cref="NetworkMatchConnectionState.Failed"/> — long enough for a
    /// healthy UDP handshake (sub-second on a reachable server), short enough that a
    /// tester with a misconfigured endpoint is not stranded in CONNECTING.</summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(8);

    private readonly INetTransport _transport;
    private readonly bool _ownsTransport;
    private readonly ClientSession _session;
    private readonly ILogger<NetworkMatchClient> _logger;
    private readonly Func<TimeSpan> _elapsedSource;
    private NetworkMatchConnectionState _state = NetworkMatchConnectionState.Connecting;
    private uint _localNetworkId;
    private byte _localTeamId;
    private WelcomeMatchModeKind _matchModeKind;
    private byte _matchRespawnsConfigured;
    private bool _isCommander;
    private WorldSnapshot? _latestSnapshot;
    private MatchOverFrame? _matchOver;
    private bool _disposed;

    /// <summary>Constructs the session over <paramref name="transport"/>. When
    /// <paramref name="ownsTransport"/> is true (runtime path), <see cref="Dispose"/>
    /// disposes the transport too; when false (test path with a shared loopback hub) the
    /// caller keeps ownership. <paramref name="elapsedSource"/> is a test seam for the
    /// connect-deadline clock — runtime leaves it null for a real stopwatch.</summary>
    public NetworkMatchClient(
        INetTransport transport,
        bool ownsTransport = true,
        ILogger<NetworkMatchClient>? logger = null,
        Func<TimeSpan>? elapsedSource = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _ownsTransport = ownsTransport;
        _logger = logger ?? NullLogger<NetworkMatchClient>.Instance;
        _elapsedSource = elapsedSource ?? CreateConnectClock();
        _session = new ClientSession(transport);
        _session.Connected += OnSessionConnected;
        _session.WelcomeReceived += OnSessionWelcome;
        _session.SnapshotReceived += OnSessionSnapshot;
        _session.MatchOverReceived += OnSessionMatchOver;
        _session.Disconnected += OnSessionDisconnected;
    }

    /// <summary>Where the client currently sits in the connect → play → drop machine.</summary>
    public NetworkMatchConnectionState State => _state;

    /// <summary>The network id the server stamped on the local peer's tank, populated
    /// when WelcomeAck arrives. Zero before that. The renderer uses it to pick the
    /// "self" entity out of the snapshot.</summary>
    public uint LocalNetworkId => _localNetworkId;

    /// <summary>The team id the server placed the local peer on (from WelcomeAck).</summary>
    public byte LocalTeamId => _localTeamId;

    /// <summary>Kind of match the server is hosting — Hungry Battles / Tactical 5v5 —
    /// surfaced from the welcome frame so the screen can label the round.</summary>
    public WelcomeMatchModeKind MatchModeKind => _matchModeKind;

    /// <summary>Initial respawn budget the server stamped on the local peer's tank. Zero
    /// for single-life modes. The authoritative count lives on the server; this value
    /// labels the round (e.g. "3 respawns") in the UI.</summary>
    public byte MatchRespawnsConfigured => _matchRespawnsConfigured;

    /// <summary>True when the server designated the local peer the commander of its team
    /// — the first peer seated on a team in a Tactical 5v5 match. False in free-for-all
    /// and for every non-first team-mate.</summary>
    public bool IsCommander => _isCommander;

    /// <summary>The most recent snapshot that arrived. <c>null</c> before the first
    /// snapshot.</summary>
    public WorldSnapshot? LatestSnapshot => _latestSnapshot;

    /// <summary>Total snapshot frames observed since the session opened. Useful for tests
    /// that assert "snapshots are flowing".</summary>
    public int SnapshotsReceived { get; private set; }

    /// <summary>The server's match-over verdict once it has arrived; <c>null</c> while
    /// the match is still being contested. The server broadcasts this exactly once, on
    /// the tick it decides the match.</summary>
    public MatchOverFrame? MatchOver => _matchOver;

    /// <summary>True once the match has ended — a shorthand for <c>MatchOver is not
    /// null</c>.</summary>
    public bool IsMatchOver => _matchOver is not null;

    /// <summary>The local player's reading of the finished match — VICTORY / DEFEAT /
    /// DRAW resolved against this peer's own network id and team. <c>null</c> until the
    /// match-over frame arrives.</summary>
    public NetworkMatchVerdict? Verdict =>
        _matchOver is { } frame ? ResolveVerdict(frame) : null;

    /// <summary>Drains one tick's worth of transport events through the session. Safe to
    /// call from the game tick + after disposal (becomes a no-op).</summary>
    public void Pump()
    {
        if (_disposed)
        {
            return;
        }

        _session.Pump();

        // A connect attempt that never receives Welcome must not strand the screen in
        // CONNECTING forever — past the deadline the client gives up to the terminal
        // Failed state, which the screen surfaces as an actionable diagnostic.
        if (_state == NetworkMatchConnectionState.Connecting && _elapsedSource() >= ConnectTimeout)
        {
            _state = NetworkMatchConnectionState.Failed;
            _logger.LogWarning(
                "Network match connect timed out after {TimeoutSeconds:0.#}s with no welcome from the server.",
                ConnectTimeout.TotalSeconds);
        }
    }

    /// <summary>Encodes <paramref name="frame"/> and sends to the server. Returns false
    /// when the session has not yet reached <see cref="NetworkMatchConnectionState.Connected"/>,
    /// when the transport rejected the send, or when the client is disposed.</summary>
    public bool SendInput(ClientInputFrame frame)
    {
        if (_disposed || _state != NetworkMatchConnectionState.Connected)
        {
            return false;
        }

        return _session.SendInput(frame);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.Connected -= OnSessionConnected;
        _session.WelcomeReceived -= OnSessionWelcome;
        _session.SnapshotReceived -= OnSessionSnapshot;
        _session.MatchOverReceived -= OnSessionMatchOver;
        _session.Disconnected -= OnSessionDisconnected;
        _session.Dispose();
        if (_ownsTransport)
        {
            _transport.Dispose();
        }
    }

    /// <summary>Default monotonic elapsed source: a <see cref="Stopwatch"/> started now,
    /// reporting time since the connect attempt began.</summary>
    private static Func<TimeSpan> CreateConnectClock()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed;
    }

    private void OnSessionConnected(ConnectionId peer)
    {
        _logger.LogInformation("Network match client connected to server peer {Peer}.", peer);
    }

    private void OnSessionWelcome(WelcomeFrame welcome)
    {
        // Welcome is the canonical "match boundary" — a fresh frame arrives either at
        // first connect or whenever the server resets the match for the next round. Clear
        // the prior match-over verdict + stale snapshot so the screen resumes the play
        // path on the next pump instead of holding the previous match's terminal banner.
        _matchOver = null;
        _latestSnapshot = null;
        _localNetworkId = welcome.NetworkId;
        _localTeamId = welcome.TeamId;
        _matchModeKind = welcome.ModeKind;
        _matchRespawnsConfigured = welcome.RespawnsConfigured;
        _isCommander = welcome.IsCommander;
        _state = NetworkMatchConnectionState.Connected;
        _logger.LogInformation(
            "Network match welcomed: local_network_id={NetworkId} team={Team} mode={Mode} respawns={Respawns} commander={Commander}.",
            welcome.NetworkId,
            welcome.TeamId,
            welcome.ModeKind,
            welcome.RespawnsConfigured,
            welcome.IsCommander);
    }

    private void OnSessionSnapshot(WorldSnapshot snapshot)
    {
        _latestSnapshot = snapshot;
        SnapshotsReceived++;
    }

    private void OnSessionMatchOver(MatchOverFrame frame)
    {
        _matchOver = frame;
        _logger.LogInformation(
            "Network match over: result={Result}, winner_network_id={WinnerNetworkId}, winner_team={WinnerTeam}.",
            frame.Result,
            frame.WinnerNetworkId,
            frame.WinnerTeam);
    }

    private void OnSessionDisconnected(ConnectionId peer)
    {
        _state = NetworkMatchConnectionState.Disconnected;
        _logger.LogInformation("Network match client lost server peer {Peer}.", peer);
    }

    /// <summary>Reads the objective <see cref="MatchOverFrame"/> against this peer's own
    /// identity: a free-for-all win is judged by network id, a team win by team id.</summary>
    private NetworkMatchVerdict ResolveVerdict(MatchOverFrame frame)
    {
        if (frame.Result == MatchOverResult.Draw)
        {
            return NetworkMatchVerdict.Draw;
        }

        var localWon = frame.WinnerNetworkId != 0
            ? frame.WinnerNetworkId == _localNetworkId
            : frame.WinnerTeam == _localTeamId;
        return localWon ? NetworkMatchVerdict.Victory : NetworkMatchVerdict.Defeat;
    }
}
