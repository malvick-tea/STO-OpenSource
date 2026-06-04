using System.Threading;
using System.Threading.Tasks;
using Garupan.Client.Core.Application;
using Garupan.Client.Core.Bootstrap;
using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Match.Network;
using Garupan.Client.Ui.Navigation;
using Garupan.Client.Ui.Screens.MainMenu;
using Garupan.Content;
using Microsoft.Extensions.Logging;
using Opus.Engine.Input;
using Opus.Engine.Ui;

namespace Garupan.Client.Windows.Bootstrap;

/// <summary>
/// Final boot stage. Replaces the splash with the main menu via a fade. Order 1100
/// puts it after every services-band stage. M2a adds an artificial dwell so users
/// can actually see the splash — boot is currently so cheap that without it the
/// transition is sub-frame.
/// </summary>
public sealed class InitialScreenStage : IBootStage
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
    private readonly ILogger<InitialScreenStage> _logger;

    public InitialScreenStage(
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
        ILoggerFactory loggerFactory,
        ILogger<InitialScreenStage> logger)
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
        _matchClientFactory = new NetworkMatchClientFactory(loggerFactory);
        _logger = logger;
    }

    public string Name => "InitialScreen";

    public int Order => 1100;

    public async Task ExecuteAsync(BootContext ctx, CancellationToken ct)
    {
        await ctx.MainThread.InvokeAsync(() =>
            _stack.Replace(
                new MainMenuScreen(
                    _stack,
                    _exit,
                    _l10n,
                    _modelLoader,
                    _modelRenderer,
                    _matchSceneRenderer,
                    _mouseMode,
                    _campaign,
                    _crewRoster,
                    _progress,
                    _settings,
                    _matchModes,
                    _matchClientFactory),
                ScreenTransition.Fade(0.5f))).ConfigureAwait(false);
        _logger.LogInformation("Replaced splash with main menu (fade 0.5s).");
    }
}
