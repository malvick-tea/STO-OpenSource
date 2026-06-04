using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Match;
using Garupan.Client.Ui.Navigation;
using Garupan.Client.Ui.Screens.Campaign;
using Garupan.Content;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;
using Opus.Localisation;

namespace Garupan.Client.Ui.Screens.Match;

/// <summary>
/// Post-match Victory / Defeat splash. A win marks the mission complete in
/// <see cref="CampaignProgressService"/> (persisted to disk), which unlocks the next
/// node on the campaign graph. Dismiss (click / Esc) collapses the briefing → match →
/// result sub-flow back to the campaign screen in one step.
///
/// In M5+ this grows replay/share buttons + cinematic outro hooks. Phase-0 keeps it
/// austere.
/// </summary>
public sealed class MissionResultScreen : IScreen
{
    private static readonly Color Bg          = new(8, 10, 14, 255);
    private static readonly Color Fg          = new(220, 226, 240, 255);
    private static readonly Color Dim         = new(140, 148, 168, 255);
    private static readonly Color VictoryFg   = new(120, 220, 150, 255);
    private static readonly Color DefeatFg    = new(220, 70, 80, 255);
    private static readonly Color Crimson     = new(196, 36, 56, 255);

    private readonly ScreenStack _stack;
    private readonly MissionSpec _mission;
    private readonly MatchOutcome _outcome;
    private readonly LocalizationService _l10n;
    private readonly CampaignProgressService _progress;

    public MissionResultScreen(
        ScreenStack stack,
        MissionSpec mission,
        MatchOutcome outcome,
        LocalizationService l10n,
        CampaignProgressService progress)
    {
        _stack = Ensure.NotNull(stack);
        _mission = Ensure.NotNull(mission);
        _outcome = outcome;
        _l10n = Ensure.NotNull(l10n);
        _progress = Ensure.NotNull(progress);
    }

    public void OnEnter()
    {
        // A win persists progress; idempotent, so a re-shown screen is harmless.
        if (_outcome == MatchOutcome.Victory)
        {
            _progress.MarkComplete(_mission.Id);
        }
    }

    public void OnExit()
    {
    }

    public void Update(GameTime time, IInputSource input)
    {
        _ = time;
        if (!input.IsKeyPressed(Key.Escape) && !input.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        // Unwind the whole match sub-flow (result + match + briefing) back to the
        // campaign graph. Falls back to a plain pop if the graph isn't on the stack.
        var transition = ScreenTransition.Fade(0.3f);
        if (!_stack.PopTo<CampaignScreen>(transition))
        {
            _stack.Pop(transition);
        }
    }

    public void Render(IDrawSurface surface)
    {
        surface.Clear(Bg);
        var w = surface.Width;
        var h = surface.Height;

        var headline = _outcome == MatchOutcome.Victory ? "VICTORY" : "DEFEAT";
        var headlineColor = _outcome == MatchOutcome.Victory ? VictoryFg : DefeatFg;

        var headlineSize = surface.MeasureText(headline, 96);
        surface.DrawText(headline, (w - headlineSize) / 2, (h / 2) - 80, 96, headlineColor);

        surface.FillRect((w - 280) / 2, (h / 2) + 40, 280, 4, Crimson);

        var title = _l10n.T(TranslationKey.Of(_mission.TitleKey));
        surface.DrawText(_mission.EpisodeReference, (w - surface.MeasureText(_mission.EpisodeReference, 16)) / 2, (h / 2) + 60, 16, Dim);
        surface.DrawText(title, (w - surface.MeasureText(title, 22)) / 2, (h / 2) + 92, 22, Fg);

        const string Hint = "Click anywhere / Esc to return to campaign";
        var hintSize = surface.MeasureText(Hint, 14);
        surface.DrawText(Hint, (w - hintSize) / 2, h - 40, 14, Dim);
    }
}
