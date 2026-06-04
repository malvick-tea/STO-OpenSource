using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Navigation;
using Garupan.Client.Ui.Screens.Campaign.Briefing;
using Garupan.Content;
using Garupan.Localisation;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Campaign;

/// <summary>
/// Pre-match briefing screen. Two-column layout: meta on the left
/// (<see cref="BriefingLeftPanel"/>), long-form copy on the right
/// (<see cref="BriefingRightPanel"/>), DEPLOY button on the left-column bottom
/// (<see cref="Briefing.DeployButton"/>). M5+ adds the cinematic intro between
/// DEPLOY click and the match itself.
///
/// This file is orchestration only — top bar + hint + input wiring.
/// </summary>
public sealed class MissionBriefingScreen : IScreen
{
    private readonly ScreenStack _stack;
    private readonly LocalizationService _l10n;
    private readonly MissionSpec _mission;
    private readonly CampaignProgressService _progress;
    private readonly SettingsService _settings;
    private readonly BriefingLeftPanel _leftPanel;
    private readonly BriefingRightPanel _rightPanel;
    private readonly DeployButton _deployButton;

    private bool _deployHovered;
    private int _lastWidth;
    private int _lastHeight;

    public MissionBriefingScreen(
        ScreenStack stack,
        LocalizationService l10n,
        IModelLoader modelLoader,
        IModelRenderer modelRenderer,
        MissionSpec mission,
        CampaignProgressService progress,
        SettingsService settings)
    {
        _stack = Ensure.NotNull(stack);
        _l10n = Ensure.NotNull(l10n);
        // The model loader / renderer are reserved for the cinematic preview that lands
        // in M5+ on the right column; they're threaded through CampaignScreen so the
        // composition surface is stable when that work begins.
        _ = Ensure.NotNull(modelLoader);
        _ = Ensure.NotNull(modelRenderer);
        _mission = Ensure.NotNull(mission);
        _progress = Ensure.NotNull(progress);
        _settings = Ensure.NotNull(settings);
        _leftPanel = new BriefingLeftPanel(_l10n);
        _rightPanel = new BriefingRightPanel(_l10n);
        _deployButton = new DeployButton(_l10n);
    }

    public void OnEnter()
    {
    }

    public void OnExit()
    {
    }

    public void Update(GameTime time, IInputSource input)
    {
        _ = time;
        if (input.IsKeyPressed(Key.Escape))
        {
            _stack.Pop(ScreenTransition.Fade(0.25f));
            return;
        }

        var (mx, my) = input.MousePosition;
        _deployHovered = _deployButton.HitTest(mx, my, _lastWidth, _lastHeight);

        if (_deployHovered && input.IsMouseButtonPressed(MouseButton.Left))
        {
            // Campaign matches are v2.0 scope (after multiplayer-first v1.0). The prior
            // top-down 2D MatchScreen was scaffolding — surface a placeholder pointing at
            // the real timeline instead. See [[garupan-release-goals-2026-2027]].
            _stack.Push(new MissionInDevelopmentScreen(_stack), ScreenTransition.Fade(0.3f));
        }
    }

    public void Render(IDrawSurface surface)
    {
        surface.Clear(BriefingPalette.Background);
        _lastWidth = surface.Width;
        _lastHeight = surface.Height;

        // Top bar.
        surface.FillRect(0, 0, surface.Width, 56, BriefingPalette.Panel);
        surface.DrawText(_l10n.T(L10nKeys.Campaign.BriefingTitle), 24, 14, 22, BriefingPalette.Foreground);
        surface.FillRect(0, 56, surface.Width, 2, BriefingPalette.Crimson);

        _leftPanel.Render(surface, _mission);
        _rightPanel.Render(surface, _mission);
        _deployButton.Render(surface, _deployHovered);

        // Hint.
        const string Hint = "Esc to back to campaign";
        var hintWidth = surface.MeasureText(Hint, 12);
        surface.DrawText(Hint, (surface.Width - hintWidth) / 2, surface.Height - 22, 12, BriefingPalette.Dim);
    }
}
