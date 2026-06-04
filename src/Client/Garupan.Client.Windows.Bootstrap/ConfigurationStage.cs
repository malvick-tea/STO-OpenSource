using System.Threading;
using System.Threading.Tasks;
using Garupan.Client.Core.Bootstrap;
using Garupan.Client.Core.Services;
using Microsoft.Extensions.Logging;

namespace Garupan.Client.Windows.Bootstrap;

/// <summary>
/// Loads <c>user://settings.gsav</c> into <see cref="SettingsService"/>. Order 50 puts
/// it in the infra band — runs before any feature stage that might need a configured
/// resolution / locale / volume.
/// </summary>
public sealed class ConfigurationStage : IBootStage
{
    private readonly SettingsService _settings;
    private readonly ILogger<ConfigurationStage> _logger;

    public ConfigurationStage(SettingsService settings, ILogger<ConfigurationStage> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public string Name => "Configuration";

    public int Order => 50;

    public async Task ExecuteAsync(BootContext ctx, CancellationToken ct)
    {
        _ = ctx;
        await _settings.LoadAsync(ct).ConfigureAwait(false);
        _logger.LogDebug(
            "Settings ready: locale={Locale}, master={Master:0.00}",
            _settings.Current.Locale,
            _settings.Current.MasterVolume);
    }
}
