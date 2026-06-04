using System;
using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Match.Network;
using Garupan.Client.Ui.Navigation;
using Garupan.Sim.Protocol;
using Garupan.Sim.Snapshot;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Match;

/// <summary>
/// First-cut network match screen. Owns one <see cref="NetworkMatchClient"/> — drains
/// its events on Update, sends input frames built from the keyboard + mouse, renders the
/// latest snapshot as dots through a shared <see cref="MatchViewport"/>.
/// </summary>
/// <remarks>
/// <para>
/// Visuals are intentionally minimal: a single-line status, a grid + tank dots + projectile
/// dots, a hint line. The Pillar-2 runtime renderer ports onto the snapshot stream in
/// a later phase — the goal of Phase 37 is to close the end-to-end loop (lobby → connect
/// → tick → render → input → send) on top of a real UDP server. Drawing primitives live
/// in <see cref="NetworkMatchRenderer"/>; input → frame conversion in
/// <see cref="NetworkMatchInputCapture"/>; this screen is composition + lifecycle.
/// </para>
/// <para>
/// Esc disconnects the session + pops the screen — back to the lobby. Three terminal
/// states hold the screen until the player acts: a connect-timeout (Failed), an
/// established-link drop (Disconnected), and a decided match (the VICTORY / DEFEAT /
/// DRAW verdict banner). Failed + Disconnected are recoverable in place — Enter opens a
/// fresh client against the same endpoint, sparing the player a lobby round-trip.
/// </para>
/// </remarks>
public sealed class NetworkMatchScreen : IScreen, IDisposable
{
    private readonly ScreenStack _stack;
    private readonly SettingsService _settings;
    private readonly IMatchSceneRenderer _matchSceneRenderer;
    private readonly IMouseModeService _mouseMode;

    /// <summary>The mode the player picked on the lobby card. Compared against the
    /// server's reported <see cref="NetworkMatchClient.MatchModeKind"/> once Welcome
    /// arrives — a difference raises the mode-mismatch notice.</summary>
    private readonly WelcomeMatchModeKind _expectedMode;

    /// <summary>Factory closure the lobby threads in to mint a fresh client when the
    /// player retries from a terminal state. Captures the original endpoint so the retry
    /// reaches the same server; null in test paths that drive the screen directly.</summary>
    private readonly Func<NetworkMatchClient>? _clientFactory;

    /// <summary>Current network client — mutable because the retry path swaps it for a
    /// fresh one when the player presses Enter on a Failed / Disconnected banner.</summary>
    private NetworkMatchClient _client;
    private readonly NetworkMatchCameraControl _camera = new();

    /// <summary>The local player's drive command from the most recently sent input frame,
    /// forwarded to the scene plan so the self tank's engine + track audio follow drive
    /// effort rather than hull translation. Zero whenever no input was sent this frame
    /// (terminal banner, paused, pre-connect).</summary>
    private float _localThrottle;

    /// <summary>The local player's steering command from the most recently sent input frame.
    /// Forwarded alongside <see cref="_localThrottle"/> so a stationary pivot turn (steering
    /// only, no throttle) still drives the engine + track audio. Zero whenever no input was
    /// sent this frame.</summary>
    private float _localSteering;

    /// <summary>Frame-sliced loader for the heavy 3D scene assets, begun on enter. Advanced one
    /// step per frame while <see cref="_loaded"/> is false so the window keeps pumping and the
    /// loading bar fills, instead of freezing the first render for several seconds while the city
    /// mesh, atlases, and pipeline state upload synchronously.</summary>
    private IMatchScenePreload? _preload;

    /// <summary>False until the scene preload has finished; the screen shows the loading bar and
    /// suppresses gameplay input until it flips true.</summary>
    private bool _loaded;

    /// <summary>Set when the player presses Esc to leave. The ScreenStack keeps Update/Render-ing
    /// the outgoing screen for the pop fade's duration; this guard stops the screen from re-arming
    /// the looping vehicle audio that <see cref="IMatchSceneRenderer.EndMatch"/> just silenced —
    /// without it, engine + track sound leaks into the menu behind the fade.</summary>
    private bool _leaving;

    private bool _disposed;

    public NetworkMatchScreen(
        ScreenStack stack,
        NetworkMatchClient client,
        SettingsService settings,
        WelcomeMatchModeKind expectedMode,
        IMatchSceneRenderer matchSceneRenderer,
        IMouseModeService mouseMode,
        Func<NetworkMatchClient>? clientFactory = null)
    {
        _stack = Ensure.NotNull(stack);
        _client = Ensure.NotNull(client);
        _settings = Ensure.NotNull(settings);
        _expectedMode = expectedMode;
        _matchSceneRenderer = Ensure.NotNull(matchSceneRenderer);
        _mouseMode = Ensure.NotNull(mouseMode);
        _clientFactory = clientFactory;
    }

    /// <summary>The currently-active network client. Exposed for tests that need to
    /// observe the retry-driven swap; runtime callers leave it alone.</summary>
    internal NetworkMatchClient Client => _client;

    public void OnEnter()
    {
        _mouseMode.SetRelativeMouseMode(true);
        _preload = _matchSceneRenderer.BeginPreload();
    }

    public void OnExit()
    {
        _mouseMode.SetRelativeMouseMode(false);
        _matchSceneRenderer.EndMatch();
        _client.Dispose();
    }

    public void Update(GameTime time, IInputSource input)
    {
        // Dismissed via Esc: the pop fade keeps this screen alive for ~0.25 s, but it must stop
        // driving the sim, audio, and scene now (see _leaving) so the silenced audio stays silent.
        if (_leaving)
        {
            return;
        }

        _client.Pump();

        if (input.IsKeyPressed(Key.Escape))
        {
            BeginLeaving();
            return;
        }

        // Heavy scene assets stream in over several frames behind a progress bar; the network
        // connect runs in parallel through Pump above. Gameplay input is suppressed until ready.
        if (!_loaded)
        {
            AdvanceSceneLoad();
            return;
        }

        UpdateGameplay(time, input);
    }

    /// <summary>Begins the leave-to-lobby pop: silences the match audio + scene, then starts the
    /// fade. The <see cref="_leaving"/> guard keeps later frames of the fade from re-arming either.</summary>
    private void BeginLeaving()
    {
        _leaving = true;
        _matchSceneRenderer.EndMatch();
        _stack.Pop(ScreenTransition.Fade(0.25f));
    }

    /// <summary>Runs one scene-preload step per frame; flips <see cref="_loaded"/> once the preload
    /// reports complete (or immediately when the renderer had nothing to slice).</summary>
    private void AdvanceSceneLoad()
    {
        if (_preload is null || !_preload.Advance())
        {
            _loaded = true;
        }
    }

    private void UpdateGameplay(GameTime time, IInputSource input)
    {
        // Cleared each frame; only a sent input frame re-arms them. A held banner therefore reports
        // no drive, so the audio idles instead of revving phantom-ly.
        _localThrottle = 0f;
        _localSteering = 0f;

        // RMB-drag orbits the camera without re-aiming the turret. Releasing RMB smoothly restores
        // the view behind the barrel; wheel zoom stays narrow so the tank remains close.
        var snapshot = _client.LatestSnapshot;
        var self = NetworkMatchInputCapture.FindSelf(snapshot, _client.LocalNetworkId);
        var (mouseDeltaX, mouseDeltaY) = input.MouseDelta;
        var orbitHeld = input.IsMouseButtonDown(MouseButton.Right);
        _camera.Update(
            mouseDeltaX,
            mouseDeltaY,
            orbitHeld,
            input.MouseWheelDelta,
            time.TickIntervalSeconds,
            self?.TurretYawRadians,
            self?.MinBarrelPitchRadians ?? EntitySnapshot.UnboundedMinBarrelPitchRadians,
            self?.MaxBarrelPitchRadians ?? EntitySnapshot.UnboundedMaxBarrelPitchRadians);

        // A decided match or a dropped link is terminal: the screen holds its banner (the verdict,
        // or the DISCONNECTED / FAILED status line) and waits for the player to act. Enter on a
        // retryable terminal state opens a fresh client against the same endpoint; the verdict
        // banner is unaffected because IsMatchOver lives on a Connected link.
        if (_client.IsMatchOver || _client.State != NetworkMatchConnectionState.Connected)
        {
            if (input.IsKeyPressed(Key.Enter) && CanRetry())
            {
                _client = NetworkMatchClientReplacement.TryReplace(_client, _clientFactory);
            }

            return;
        }

        var nextInputTick = (ulong)(snapshot?.Tick.Value ?? 0L) + 1UL;
        var frame = NetworkMatchInputCapture.BuildInputFrame(
            nextInputTick,
            _client.LocalNetworkId,
            input,
            _settings.Current.Bindings,
            _camera.ResolveTurretTarget(self?.TurretYawRadians ?? 0f),
            _camera.BarrelPitchRadians);
        _localThrottle = frame.Throttle;
        _localSteering = frame.Steering;
        _client.SendInput(frame);
    }

    public void Render(IDrawSurface surface)
    {
        surface.Clear(NetworkMatchPalette.Background);

        // Leaving: skip the scene composite so the renderer's vehicle audio is not re-armed during
        // the pop fade. The ScreenStack's fade cover handles the visual hand-off.
        if (_leaving)
        {
            return;
        }

        // Still streaming assets: show the progress bar instead of an unprepared (frozen) scene.
        if (!_loaded)
        {
            MatchLoadingView.Draw(
                surface,
                _preload?.Progress ?? 0f,
                _preload?.StageLabel ?? string.Empty,
                ResolveLoadingSubStatus());
            return;
        }

        // Sky + ground horizon backdrop, drawn before the 3D scene: the scene composites
        // its own sky transparent, so this 2D backdrop fills the field behind the tanks.
        NetworkMatchSkyBackdrop.Draw(surface);

        // 3D scene next, then the 2D chrome composites on top.
        _matchSceneRenderer.Render(NetworkMatchSceneProjection.Build(
            _client.LatestSnapshot,
            _client.LocalNetworkId,
            _camera.YawRadians,
            _camera.PitchRadians,
            _camera.DistanceMeters,
            _camera.BarrelPitchRadians,
            _localThrottle,
            _localSteering));

        DrawTopBar(surface);
        NetworkMatchRenderer.DrawHint(surface);

        // A live mismatch (the lobby card's mode ≠ the server's reported mode) means the
        // configured endpoint points at the wrong server — surface it, don't block play.
        var modeMismatch = _client.State == NetworkMatchConnectionState.Connected
            && _client.MatchModeKind != _expectedMode;
        if (modeMismatch)
        {
            NetworkMatchRenderer.DrawModeMismatch(
                surface,
                NetworkMatchModeText.Label(_expectedMode),
                NetworkMatchModeText.Label(_client.MatchModeKind));
        }

        if (_client.Verdict is { } verdict)
        {
            NetworkMatchRenderer.DrawVerdict(surface, verdict);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _mouseMode.SetRelativeMouseMode(false);
        _matchSceneRenderer.EndMatch();
        _client.Dispose();
    }

    private void DrawTopBar(IDrawSurface surface)
    {
        surface.FillRect(0, 0, surface.Width, NetworkMatchPalette.TopBarHeight, NetworkMatchPalette.Panel);
        var status = ResolveStatusText();
        surface.DrawText(status, 16, 10, NetworkMatchPalette.StatusFontSize, ResolveStatusColor());
        const string LeaveHint = "Esc to leave";
        var helpWidth = surface.MeasureText(LeaveHint, NetworkMatchPalette.HintFontSize);
        surface.DrawText(
            LeaveHint,
            surface.Width - helpWidth - 16,
            12,
            NetworkMatchPalette.HintFontSize,
            NetworkMatchPalette.Dim);
    }

    /// <summary>The connection state shown beneath the loading bar, so a tester knows whether the
    /// (freeze-free) wait is the asset stream or the network handshake.</summary>
    private string ResolveLoadingSubStatus() => _client.State switch
    {
        NetworkMatchConnectionState.Connected => "Connected — entering battle…",
        NetworkMatchConnectionState.Failed => "Connection failed — check the Multiplayer settings",
        NetworkMatchConnectionState.Disconnected => "Disconnected from server",
        _ => $"Connecting to {NetworkMatchModeText.Label(_expectedMode)}…",
    };

    private string ResolveStatusText()
    {
        // A decided match keeps the link alive (state stays Connected) — the verdict is
        // a frame, not a disconnect — so the match-over check comes before the switch.
        if (_client.IsMatchOver)
        {
            return "MATCH OVER  ·  Esc to leave";
        }

        // The commander role is surfaced as a status-line tag for now; the Pillar-2
        // handdrawn-map briefing surface rides a later phase.
        var commanderTag = _client.IsCommander ? "  ·  COMMANDER" : string.Empty;
        var leaveHint = CanRetry() ? "Enter to RETRY  ·  Esc to leave" : "Esc to leave";
        return _client.State switch
        {
            NetworkMatchConnectionState.Connecting =>
                $"CONNECTING — {NetworkMatchModeText.Label(_expectedMode)}…",
            NetworkMatchConnectionState.Connected =>
                $"{NetworkMatchModeText.Label(_client.MatchModeKind)}{commanderTag}  ·  id={_client.LocalNetworkId}  ·  team={_client.LocalTeamId}  ·  respawns={_client.MatchRespawnsConfigured}  ·  snapshots={_client.SnapshotsReceived}",
            NetworkMatchConnectionState.Disconnected =>
                $"DISCONNECTED  ·  {leaveHint}",
            NetworkMatchConnectionState.Failed =>
                $"CONNECTION FAILED  ·  check the Multiplayer settings  ·  {leaveHint}",
            _ => "?",
        };
    }

    private Color ResolveStatusColor() => _client.State switch
    {
        NetworkMatchConnectionState.Disconnected => NetworkMatchPalette.Warn,
        NetworkMatchConnectionState.Failed => NetworkMatchPalette.Warn,
        _ => NetworkMatchPalette.Foreground,
    };

    /// <summary>True when the player can press Enter to swap the current client for a
    /// fresh one against the same endpoint — only on a retryable terminal state, and
    /// only when the lobby threaded in a factory closure (test paths typically omit it).</summary>
    private bool CanRetry() =>
        _clientFactory is not null && NetworkMatchTerminalState.IsRetryable(_client.State);
}
