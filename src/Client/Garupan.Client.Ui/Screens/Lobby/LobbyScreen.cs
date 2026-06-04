using System.Collections.Generic;
using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Match.Network;
using Garupan.Client.Ui.Navigation;
using Garupan.Client.Ui.Screens.Match;
using Garupan.Content;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Lobby;

/// <summary>
/// PLAY → Lobby screen. Shows every match mode in <see cref="MatchModeCatalog"/> as a
/// card; clicking one dials the local test match server and pushes a
/// <see cref="NetworkMatchScreen"/>. The lobby is where the local test modes (Hungry
/// Battles, Tactical 5v5) become legible to the player, and — as of Phase 37 — the
/// surface that opens a live <see cref="NetworkMatchClient"/>.
/// </summary>
/// <remarks>
/// Single responsibility — orchestrate input + screen transitions + a single render
/// pass. Geometry, hit-test, and card pixels live in <see cref="MatchModeListView"/> +
/// <see cref="MatchModeCardRenderer"/>; localised strings come pre-resolved through
/// <see cref="LobbyTranslationsFactory"/>; transport creation lives in
/// <see cref="NetworkMatchClientFactory"/>. Server-side mode selection (the catalogue's
/// per-mode rules) is Phase 38+ — for now every card connects to the same host.
/// </remarks>
public sealed class LobbyScreen : IScreen
{
    private readonly ScreenStack _stack;
    private readonly LocalizationService _l10n;
    private readonly MatchModeCatalog _catalog;
    private readonly NetworkMatchClientFactory _matchClientFactory;
    private readonly SettingsService _settings;
    private readonly IMatchSceneRenderer _matchSceneRenderer;
    private readonly IMouseModeService _mouseMode;
    private readonly ILogger<LobbyScreen> _logger;
    private readonly MatchModeListView _listView;
    private readonly MatchModeCardRenderer _cardRenderer;
    private int _hoveredIndex = -1;

    public LobbyScreen(
        ScreenStack stack,
        LocalizationService l10n,
        MatchModeCatalog catalog,
        NetworkMatchClientFactory matchClientFactory,
        SettingsService settings,
        IMatchSceneRenderer matchSceneRenderer,
        IMouseModeService mouseMode,
        ILogger<LobbyScreen>? logger = null)
    {
        _stack = Ensure.NotNull(stack);
        _l10n = Ensure.NotNull(l10n);
        _catalog = Ensure.NotNull(catalog);
        _matchClientFactory = Ensure.NotNull(matchClientFactory);
        _settings = Ensure.NotNull(settings);
        _matchSceneRenderer = Ensure.NotNull(matchSceneRenderer);
        _mouseMode = Ensure.NotNull(mouseMode);
        _logger = logger ?? NullLogger<LobbyScreen>.Instance;
        _cardRenderer = new MatchModeCardRenderer();
        _listView = new MatchModeListView(_cardRenderer);
    }

    public void OnEnter() => _hoveredIndex = -1;

    public void OnExit()
    {
    }

    public void Update(GameTime time, IInputSource input)
    {
        _ = time;
        var (mx, my) = input.MousePosition;
        _hoveredIndex = _listView.HitTest(mx, my);

        if (input.IsKeyPressed(Key.Escape))
        {
            _stack.Pop(ScreenTransition.Fade(0.25f));
            return;
        }

        if (_hoveredIndex >= 0 && input.IsMouseButtonPressed(MouseButton.Left))
        {
            LaunchNetworkMatch(_catalog.Modes[_hoveredIndex]);
        }
    }

    /// <summary>Opens a live client against the local test match server and hands it to
    /// a <see cref="NetworkMatchScreen"/>. The endpoint is resolved from
    /// <see cref="SettingsService.Current"/>.Multiplayer (host + port) — a local test
    /// tester points this at the dev-hosted server via the Settings → Multiplayer
    /// sub-screen. The clicked card's <paramref name="mode"/> rides along as the
    /// <em>expected</em> mode so the match screen can flag a server it joined that hosts
    /// a different mode than the card promised. A factory closure threaded into the
    /// screen lets a tester press Enter on a Failed / Disconnected banner to re-open a
    /// fresh client against the same endpoint without backing out to the lobby.</summary>
    private void LaunchNetworkMatch(MatchMode mode)
    {
        var expectedMode = NetworkMatchModeText.FromContent(mode.Kind);
        var endpoint = NetworkMatchEndpointResolver.Resolve(_settings.Current.Multiplayer, expectedMode, _logger);
        _stack.Push(
            new NetworkMatchScreen(
                _stack,
                _matchClientFactory.Create(endpoint),
                _settings,
                expectedMode,
                _matchSceneRenderer,
                _mouseMode,
                clientFactory: () => _matchClientFactory.Create(endpoint)),
            ScreenTransition.Fade(0.3f));
    }

    public void Render(IDrawSurface surface)
    {
        var translations = LobbyTranslationsFactory.Resolve(_l10n);
        var cards = ResolveCards();

        _listView.Layout(surface.Width, cards.Count);

        DrawChrome(surface, translations);
        _listView.Render(surface, cards, _hoveredIndex, translations);
        DrawClosedAlphaFooter(surface, translations);
    }

    private IReadOnlyList<ResolvedMatchModeCard> ResolveCards()
    {
        var modes = _catalog.Modes;
        var translations = LobbyTranslationsFactory.Resolve(_l10n, modes);
        var resolved = new List<ResolvedMatchModeCard>(modes.Count);
        for (var i = 0; i < modes.Count; i++)
        {
            resolved.Add(new ResolvedMatchModeCard(modes[i], translations[i].Name, translations[i].Summary));
        }

        return resolved;
    }

    private static void DrawChrome(IDrawSurface surface, LobbyTranslations translations)
    {
        surface.Clear(LobbyPalette.Background);

        // Top bar — mirrors main-menu chrome so navigating in from PLAY feels seamless.
        surface.FillRect(0, 0, surface.Width, LobbyPalette.TopBarHeight, LobbyPalette.Panel);
        surface.DrawText(translations.Title, 24, 18, 18, LobbyPalette.Foreground);
        surface.DrawText("Esc to back out.", surface.Width - 160, 18, 14, LobbyPalette.Dim);

        // Title band.
        var titleWidth = surface.MeasureText(translations.Title, LobbyPalette.TitleFontSize);
        surface.DrawText(
            translations.Title,
            (surface.Width - titleWidth) / 2,
            LobbyPalette.TitleY,
            LobbyPalette.TitleFontSize,
            LobbyPalette.Foreground);
        surface.FillRect(
            (surface.Width - 240) / 2,
            LobbyPalette.TitleY + LobbyPalette.TitleFontSize + 6,
            240,
            3,
            LobbyPalette.Crimson);

        // Subtitle hint.
        var hintWidth = surface.MeasureText(translations.Hint, LobbyPalette.SubtitleFontSize);
        surface.DrawText(
            translations.Hint,
            (surface.Width - hintWidth) / 2,
            LobbyPalette.TitleY + LobbyPalette.TitleFontSize + 18,
            LobbyPalette.SubtitleFontSize,
            LobbyPalette.Dim);
    }

    private static void DrawClosedAlphaFooter(IDrawSurface surface, LobbyTranslations translations)
    {
        var width = surface.MeasureText(translations.ClosedAlpha, LobbyPalette.FooterFontSize);
        surface.DrawText(
            translations.ClosedAlpha,
            (surface.Width - width) / 2,
            surface.Height - LobbyPalette.FooterMarginY,
            LobbyPalette.FooterFontSize,
            LobbyPalette.Dim);
    }
}
