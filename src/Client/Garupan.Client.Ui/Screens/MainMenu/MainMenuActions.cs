using Garupan.Client.Core.Application;
using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Match.Network;
using Garupan.Client.Ui.Navigation;
using Garupan.Client.Ui.Screens.Campaign;
using Garupan.Client.Ui.Screens.Commander;
using Garupan.Client.Ui.Screens.Garage;
using Garupan.Client.Ui.Screens.Lobby;
using Garupan.Client.Ui.Screens.Settings;
using Garupan.Content;
using Opus.Engine.Input;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.MainMenu;

/// <summary>
/// Routes an activated nav item to the right screen push (or process exit). Pulled out
/// of <see cref="MainMenuScreen"/> so the switch grows independently of the screen's
/// rendering / hit-test concerns — every new menu entry is a one-line case added here,
/// plus an entry in <see cref="MainMenuNavList.Items"/>.
/// </summary>
public sealed class MainMenuActions
{
    private readonly ScreenStack _stack;
    private readonly IExitService _exit;
    private readonly LocalizationService _l10n;
    private readonly IModelLoader _modelLoader;
    private readonly IModelRenderer _modelRenderer;
    private readonly IMatchSceneRenderer _matchSceneRenderer;
    private readonly IMouseModeService _mouseMode;
    private readonly CampaignSpec _campaign;
    private readonly CrewRoster _crewRoster;
    private readonly CampaignProgressService _progress;
    private readonly SettingsService _settings;
    private readonly MatchModeCatalog _matchModes;
    private readonly NetworkMatchClientFactory _matchClientFactory;

    public MainMenuActions(
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
        _stack = stack;
        _exit = exit;
        _l10n = l10n;
        _modelLoader = modelLoader;
        _modelRenderer = modelRenderer;
        _matchSceneRenderer = matchSceneRenderer;
        _mouseMode = mouseMode;
        _campaign = campaign;
        _crewRoster = crewRoster;
        _progress = progress;
        _settings = settings;
        _matchModes = matchModes;
        _matchClientFactory = matchClientFactory;
    }

    public void Activate(string itemId)
    {
        switch (itemId)
        {
            case "PLAY":
                _stack.Push(
                    new LobbyScreen(_stack, _l10n, _matchModes, _matchClientFactory, _settings, _matchSceneRenderer, _mouseMode),
                    ScreenTransition.Fade(0.3f));
                break;

            case "CAMPAIGN":
                _stack.Push(
                    new CampaignScreen(_stack, _l10n, _modelLoader, _modelRenderer, _campaign, _progress, _settings),
                    ScreenTransition.Fade(0.3f));
                break;

            case "GARAGE":
                _stack.Push(
                    new GaragePlaceholderScreen(_stack, _modelLoader, _modelRenderer, _crewRoster),
                    ScreenTransition.Fade(0.3f));
                break;

            case "BATTLE PLAN":
                _stack.Push(
                    new CommanderMapScreen(_stack, _l10n),
                    ScreenTransition.Fade(0.3f));
                break;

            case "SETTINGS":
                _stack.Push(
                    new SettingsScreen(_stack, _l10n, _settings),
                    ScreenTransition.Fade(0.3f));
                break;

            case "EXIT":
                _exit.RequestExit();
                break;

            default:
                // ARCHIVE — placeholder until its real screen ships.
                _stack.Push(new ComingSoonScreen(_stack, itemId), ScreenTransition.Fade(0.25f));
                break;
        }
    }
}
