using Garupan.Client.Core.Application;
using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Match.Network;
using Garupan.Client.Ui.Navigation;
using Garupan.Content;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.MainMenu;

/// <summary>
/// Vertical-band layout matching the legacy Godot main menu. Six nav entries:
/// PLAY / GARAGE / CAMPAIGN / SETTINGS / ARCHIVE / EXIT. Hover follows mouse Y, click
/// triggers the action via <see cref="MainMenuActions"/>.
///
/// This file is orchestration only: items + hit-test + row rendering live in
/// <see cref="MainMenuNavList"/>, routing in <see cref="MainMenuActions"/>, colours in
/// <see cref="MainMenuPalette"/>.
/// </summary>
public sealed class MainMenuScreen : IScreen
{
    private readonly MainMenuNavList _navList = new();
    private readonly MainMenuActions _actions;
    private int _hoveredIndex = -1;

    public MainMenuScreen(
        ScreenStack stack,
        IExitService exit,
        LocalizationService l10n,
        IModelLoader modelLoader,
        IModelRenderer modelRenderer,
        IMatchSceneRenderer matchSceneRenderer,
        IMouseModeService mouseMode,
        CampaignSpec campaign,
        CrewRoster crewRoster,
        CampaignProgressService progress,
        SettingsService settings,
        MatchModeCatalog matchModes,
        NetworkMatchClientFactory matchClientFactory)
    {
        Ensure.NotNull(stack);
        Ensure.NotNull(exit);
        Ensure.NotNull(l10n);
        Ensure.NotNull(modelLoader);
        Ensure.NotNull(modelRenderer);
        Ensure.NotNull(matchSceneRenderer);
        Ensure.NotNull(mouseMode);
        Ensure.NotNull(campaign);
        Ensure.NotNull(crewRoster);
        Ensure.NotNull(progress);
        Ensure.NotNull(settings);
        Ensure.NotNull(matchModes);
        Ensure.NotNull(matchClientFactory);
        _actions = new MainMenuActions(
            stack,
            exit,
            l10n,
            modelLoader,
            modelRenderer,
            matchSceneRenderer,
            mouseMode,
            campaign,
            crewRoster,
            progress,
            settings,
            matchModes,
            matchClientFactory);
    }

    public void OnEnter() => _hoveredIndex = -1;

    public void OnExit()
    {
    }

    public void Update(GameTime time, IInputSource input)
    {
        _ = time;
        var (mx, my) = input.MousePosition;
        _hoveredIndex = _navList.HitTest(mx, my);

        if (_hoveredIndex >= 0 && input.IsMouseButtonPressed(MouseButton.Left))
        {
            _actions.Activate(MainMenuNavList.Items[_hoveredIndex]);
        }
    }

    public void Render(IDrawSurface surface)
    {
        surface.Clear(MainMenuPalette.Background);
        var w = surface.Width;
        var h = surface.Height;

        // Top bar — placeholder for ProfileBadge + CurrencyStrip (M2c+).
        surface.FillRect(0, 0, w, 56, MainMenuPalette.Panel);
        surface.DrawText("Player crew — guest", 24, 18, 18, MainMenuPalette.Foreground);
        surface.DrawText("0    0 ★", w - 160, 18, 18, MainMenuPalette.Dim);

        // Vertical accent on the left — same place the legacy Godot menu put its rail.
        surface.FillRect(0, 56, 4, h - 112, MainMenuPalette.Crimson);

        _navList.Render(surface, _hoveredIndex);

        // Bottom hint.
        const string Hint = "Move mouse to highlight, click to enter. Esc on subscreens to back out.";
        var hintWidth = surface.MeasureText(Hint, 14);
        surface.DrawText(Hint, (w - hintWidth) / 2, h - 40, 14, MainMenuPalette.Dim);
    }
}
