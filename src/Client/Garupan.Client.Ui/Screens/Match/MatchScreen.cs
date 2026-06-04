using System;
using System.Numerics;
using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Match;
using Garupan.Client.Ui.Navigation;
using Garupan.Client.Ui.Screens.Campaign;
using Garupan.Content;
using Garupan.Sim.Components;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Match;

/// <summary>
/// Top-down 2D viewport for an active match. Orchestrates one frame:
/// <list type="number">
/// <item><description>Pumps player input into the ECS session.</description></item>
/// <item><description>Ticks the simulation.</description></item>
/// <item><description>Hands the frozen world to a <see cref="MatchWorldRenderer"/> and the chrome to a
///     <see cref="MatchHudRenderer"/>, both sharing one <see cref="MatchViewport"/>.</description></item>
/// </list>
///
/// This screen owns no drawing logic of its own — every <c>surface.*</c> call lives in
/// one of the helper modules. Per STO concept §3.2 + §9 the 3D top-third-person camera
/// arrives in M5+; the 2D view is intentionally crude until then.
/// </summary>
public sealed class MatchScreen : IScreen, IDisposable
{
    private readonly ScreenStack _stack;
    private readonly MissionSpec _mission;
    private readonly LocalizationService _l10n;
    private readonly CampaignProgressService _progress;
    private readonly SettingsService _settings;
    private readonly PlayerInputAdapter _input = new();
    private readonly MatchWorldRenderer _worldRenderer = new();
    private readonly MatchHudRenderer _hudRenderer;
    private readonly MatchPauseOverlay _pauseOverlay;

    private MatchSession? _session;
    private MatchViewport _viewport;
    private bool _paused;
    private PauseAction _hoveredPauseAction = PauseAction.None;
    private int _lastSurfaceWidth;
    private int _lastSurfaceHeight;
    private int _mouseX;
    private int _mouseY;

    public MatchScreen(
        ScreenStack stack,
        MissionSpec mission,
        LocalizationService l10n,
        CampaignProgressService progress,
        SettingsService settings)
    {
        _stack = Ensure.NotNull(stack);
        _mission = Ensure.NotNull(mission);
        _l10n = Ensure.NotNull(l10n);
        _progress = Ensure.NotNull(progress);
        _settings = Ensure.NotNull(settings);
        _hudRenderer = new MatchHudRenderer(_mission.EpisodeReference, _settings.Current.Bindings);
        _pauseOverlay = new MatchPauseOverlay(_l10n);
    }

    public void OnEnter()
    {
        // Phase-0 every mission spawns 1 player the medium tank at south + 2 opponent tanks of the
        // appropriate school at the north-east and north-west.
        var opponentSpec = OpponentTankCatalog.For(_mission.Opponent);
        var setup = new MatchSetup(
            PlayerTank: TankRoster.VehicleMediumB,
            PlayerSpawn: new Vector2(0f, -50f),
            PlayerYaw: MathF.PI / 2f,
            Opponents: new[]
            {
                new OpponentSetup(opponentSpec, new Vector2(-30f, 30f), -MathF.PI / 2f),
                new OpponentSetup(opponentSpec, new Vector2(+30f, 30f), -MathF.PI / 2f),
            });

        _session = MatchSession.Create(setup);
    }

    public void OnExit()
    {
        _session?.Dispose();
        _session = null;
    }

    public void Update(GameTime time, IInputSource input)
    {
        _ = time;
        if (_session is null)
        {
            return;
        }

        // Esc toggles pause. While paused the simulation is not ticked — because the
        // sim advances per-tick, not by wall-clock, freezing Tick is fully deterministic.
        if (input.IsKeyPressed(Key.Escape))
        {
            _paused = !_paused;
            return;
        }

        if (_paused)
        {
            UpdatePauseMenu(input);
            return;
        }

        _input.ApplyMovement(_session, input, _settings.Current.Bindings);
        (_mouseX, _mouseY) = input.MousePosition;
        ApplyAimFromMouse(input);

        _session.Tick();

        if (_session.Outcome != MatchOutcome.InProgress)
        {
            _stack.Push(
                new MissionResultScreen(_stack, _mission, _session.Outcome, _l10n, _progress),
                ScreenTransition.Fade(0.5f));
        }
    }

    private void UpdatePauseMenu(IInputSource input)
    {
        var (mouseX, mouseY) = input.MousePosition;
        _hoveredPauseAction = MatchPauseOverlay.ActionAt(mouseX, mouseY, _lastSurfaceWidth, _lastSurfaceHeight);

        if (!input.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        switch (_hoveredPauseAction)
        {
            case PauseAction.Resume:
                _paused = false;
                break;

            case PauseAction.Abandon:
                if (!_stack.PopTo<CampaignScreen>(ScreenTransition.Fade(0.3f)))
                {
                    _stack.Pop(ScreenTransition.Fade(0.3f));
                }

                break;

            case PauseAction.None:
            default:
                break;
        }
    }

    public void Render(IDrawSurface surface)
    {
        surface.Clear(MatchPalette.Background);

        if (_session is null)
        {
            surface.DrawText("loading match...", 24, surface.Height / 2, 22, MatchPalette.Foreground);
            return;
        }

        _lastSurfaceWidth = surface.Width;
        _lastSurfaceHeight = surface.Height;
        _viewport = MatchViewport.Fit(surface.Width, surface.Height, _session.HalfExtentMeters);

        _worldRenderer.Render(surface, _session, _viewport);

        var readout = MatchHudReadout.Capture(_session);
        var (showReticle, aim) = ComputeReticleAim(readout);
        _hudRenderer.Render(surface, readout, _viewport, in aim, showReticle);

        if (_paused)
        {
            _pauseOverlay.Render(surface, _hoveredPauseAction);
        }
    }

    /// <summary>
    /// Projects the player tank's world position + the mouse aim point into screen
    /// coordinates for the reticle. The reticle is shown only when the player is alive
    /// AND the mouse is inside the world viewport, so a knocked-out player or a cursor
    /// hovering over the HUD doesn't leave a stray crosshair on the field.
    /// </summary>
    private (bool Show, ReticleAim Aim) ComputeReticleAim(MatchHudReadout readout)
    {
        if (_session is null || _paused || !readout.IsPlayerAlive || _viewport.Width <= 0)
        {
            return (false, default);
        }

        if (!_viewport.Contains(_mouseX, _mouseY))
        {
            return (false, default);
        }

        ref var playerTf = ref _session.World.Get<Transform>(_session.Player);
        var (playerSx, playerSy) = _viewport.WorldToScreen(playerTf.Position);
        var aimWorld = _viewport.ScreenToWorld(_mouseX, _mouseY);
        var range = Vector2.Distance(playerTf.Position, aimWorld);
        return (true, new ReticleAim(playerSx, playerSy, _mouseX, _mouseY, range));
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }

    private void ApplyAimFromMouse(IInputSource input)
    {
        _ = input;
        if (_viewport.Width <= 0 || _session is null || _session.Player.WorldId < 0)
        {
            return;
        }

        if (!_viewport.Contains(_mouseX, _mouseY))
        {
            return;
        }

        var mouseWorld = _viewport.ScreenToWorld(_mouseX, _mouseY);
        ref var playerTf = ref _session.World.Get<Transform>(_session.Player);
        var aimVec = mouseWorld - playerTf.Position;
        if (aimVec.LengthSquared() <= 0.001f)
        {
            return;
        }

        _input.ApplyAim(_session, MathF.Atan2(aimVec.Y, aimVec.X));
    }
}
