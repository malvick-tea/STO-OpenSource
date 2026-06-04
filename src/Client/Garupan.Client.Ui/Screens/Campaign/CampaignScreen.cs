using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Navigation;
using Garupan.Content;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Campaign;

/// <summary>
/// Campaign-graph screen. Lays out <see cref="SampleCampaign"/> as nodes connected by
/// canon order; the player hovers to read the lore summary, clicks an unlocked node to
/// push the briefing. Locked nodes (prerequisites not cleared) render greyed and ignore
/// clicks; cleared nodes show a check.
///
/// Per ADR-0008 this is the centre of the game. Future campaigns (the rival commander's prequel arc,
/// a later season, optional matches) plug in via the same <see cref="CampaignSpec"/> shape.
///
/// This file is orchestration only: layout maths live in <see cref="CampaignLayout"/>,
/// drawing in <see cref="CampaignGraphRenderer"/> + <see cref="CampaignDetailPanelRenderer"/>,
/// progression state in <see cref="CampaignProgressView"/>.
/// </summary>
public sealed class CampaignScreen : IScreen
{
    private readonly ScreenStack _stack;
    private readonly LocalizationService _l10n;
    private readonly IModelLoader _modelLoader;
    private readonly IModelRenderer _modelRenderer;
    private readonly CampaignSpec _campaign;
    private readonly CampaignProgressService _progress;
    private readonly SettingsService _settings;
    private readonly CampaignLayout _layout;
    private readonly CampaignGraphRenderer _graphRenderer;
    private readonly CampaignDetailPanelRenderer _detailRenderer;
    private readonly CampaignProgressView _progressView;

    private int _hoveredIndex = -1;
    private int _lastSurfaceWidth;
    private int _lastSurfaceHeight;

    public CampaignScreen(
        ScreenStack stack,
        LocalizationService l10n,
        IModelLoader modelLoader,
        IModelRenderer modelRenderer,
        CampaignSpec campaign,
        CampaignProgressService progress,
        SettingsService settings)
    {
        _stack = Ensure.NotNull(stack);
        _l10n = Ensure.NotNull(l10n);
        _modelLoader = Ensure.NotNull(modelLoader);
        _modelRenderer = Ensure.NotNull(modelRenderer);
        _campaign = Ensure.NotNull(campaign);
        _progress = Ensure.NotNull(progress);
        _settings = Ensure.NotNull(settings);
        _layout = new CampaignLayout(_campaign);
        _graphRenderer = new CampaignGraphRenderer(_l10n, _layout);
        _detailRenderer = new CampaignDetailPanelRenderer(_l10n, _campaign);
        _progressView = new CampaignProgressView(_campaign, _progress.Current);
    }

    public void OnEnter() => _hoveredIndex = -1;

    public void OnExit()
    {
    }

    public void Update(GameTime time, IInputSource input)
    {
        _ = time;

        // Pick up any mission cleared while a result screen was on top. CampaignProgress
        // is immutable, so Sync no-ops unless the snapshot actually changed.
        _progressView.Sync(_progress.Current);

        if (input.IsKeyPressed(Key.Escape))
        {
            _stack.Pop(ScreenTransition.Fade(0.25f));
            return;
        }

        var (mx, my) = input.MousePosition;
        _hoveredIndex = _layout.HitTest(mx, my, _lastSurfaceWidth, _lastSurfaceHeight);

        if (_hoveredIndex >= 0 &&
            _progressView.IsPlayable(_hoveredIndex) &&
            input.IsMouseButtonPressed(MouseButton.Left))
        {
            var mission = _campaign.Missions[_hoveredIndex];
            _stack.Push(
                new MissionBriefingScreen(_stack, _l10n, _modelLoader, _modelRenderer, mission, _progress, _settings),
                ScreenTransition.Fade(0.3f));
        }
    }

    public void Render(IDrawSurface surface)
    {
        surface.Clear(CampaignPalette.Background);
        // Cache surface dims so Update's hit-test (which runs without a surface) can
        // reuse them. One-frame lag on the first frame is harmless — _hoveredIndex
        // starts at -1.
        _lastSurfaceWidth = surface.Width;
        _lastSurfaceHeight = surface.Height;

        _graphRenderer.Render(surface, _hoveredIndex, _progressView);
        _detailRenderer.Render(surface, _hoveredIndex, _progressView);
    }
}
