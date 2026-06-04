using System.Collections.Generic;
using System.Globalization;
using Garupan.Client.Core.Services;
using Garupan.Content;
using Garupan.Localisation;
using Opus.Localisation;

namespace Garupan.Client.Ui.Screens.Lobby;

/// <summary>
/// Pre-resolved localised strings for the lobby chrome. Decouples renderers from
/// <see cref="LocalizationService"/> so card / list rendering can be exercised under
/// test with a plain in-memory translations record.
/// </summary>
internal sealed record LobbyTranslations(
    string Title,
    string Hint,
    string ClosedAlpha,
    string DeployLabel,
    string RespawnsTemplate,
    string CommanderLed,
    string FreeForAll);

/// <summary>Pre-resolved name + summary for a single mode card.</summary>
internal sealed record ModeTranslations(string Name, string Summary);

/// <summary>One fully-resolved mode-card payload — the underlying record plus its
/// localised name + summary text. Held in this shape by <see cref="LobbyScreen"/> so
/// the list view + card renderer never need <see cref="LocalizationService"/>.</summary>
internal sealed record ResolvedMatchModeCard(MatchMode Mode, string Name, string Summary);

/// <summary>Resolves the lobby's translation keys via a <see cref="LocalizationService"/>.
/// Pulled out of <see cref="LobbyScreen"/> so the screen orchestrator does not own the
/// per-key string-building noise.</summary>
internal static class LobbyTranslationsFactory
{
    public static LobbyTranslations Resolve(LocalizationService l10n) =>
        new(
            Title: l10n.T(L10nKeys.Lobby.Title),
            Hint: l10n.T(L10nKeys.Lobby.Hint),
            ClosedAlpha: l10n.T(L10nKeys.Lobby.ClosedAlpha),
            DeployLabel: l10n.T(L10nKeys.Lobby.Deploy),
            RespawnsTemplate: l10n.T(L10nKeys.Lobby.Respawns),
            CommanderLed: l10n.T(L10nKeys.Lobby.CommanderLed),
            FreeForAll: l10n.T(L10nKeys.Lobby.FreeForAll));

    public static IReadOnlyList<ModeTranslations> Resolve(
        LocalizationService l10n, IReadOnlyList<MatchMode> modes)
    {
        var result = new List<ModeTranslations>(modes.Count);
        foreach (var mode in modes)
        {
            result.Add(new ModeTranslations(
                Name: l10n.T(TranslationKey.Of(mode.NameKey)),
                Summary: l10n.T(TranslationKey.Of(mode.SummaryKey))));
        }

        return result;
    }

    public static string FormatRespawns(string template, int respawnLimit) =>
        string.Format(CultureInfo.InvariantCulture, template, respawnLimit);
}
