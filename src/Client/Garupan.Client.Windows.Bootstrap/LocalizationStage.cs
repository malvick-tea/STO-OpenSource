using System.Threading;
using System.Threading.Tasks;
using Garupan.Client.Core.Bootstrap;
using Garupan.Client.Core.Services;
using Microsoft.Extensions.Logging;

namespace Garupan.Client.Windows.Bootstrap;

/// <summary>
/// Reads CSV catalogues for every locale we ship today and activates whichever one the
/// settings file pointed at. Runs after <see cref="ConfigurationStage"/> but before
/// anything that wants to render localised strings.
/// </summary>
public sealed class LocalizationStage : IBootStage
{
    private static readonly string[] Locales = { "en", "ru", "ja" };

    private readonly LocalizationService _l10n;
    private readonly SettingsService _settings;
    private readonly ILogger<LocalizationStage> _logger;

    public LocalizationStage(LocalizationService l10n, SettingsService settings, ILogger<LocalizationStage> logger)
    {
        _l10n = l10n;
        _settings = settings;
        _logger = logger;
    }

    public string Name => "Localization";

    public int Order => 120;

    public async Task ExecuteAsync(BootContext ctx, CancellationToken ct)
    {
        _ = ctx;
        await _l10n.LoadAsync(Locales, ct).ConfigureAwait(false);
        _l10n.SetLocale(_settings.Current.Locale);
        _logger.LogInformation(
            "Localization ready: active='{Active}' ({Count} keys, {Total} locales loaded)",
            _l10n.Active.Locale,
            _l10n.Active.AllKeys.Count,
            _l10n.AvailableLocales.Count);
    }
}
