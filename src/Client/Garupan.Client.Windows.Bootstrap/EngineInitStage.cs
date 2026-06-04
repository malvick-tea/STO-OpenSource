using System.Threading;
using System.Threading.Tasks;
using Garupan.Client.Core.Bootstrap;
using Garupan.Client.Core.Services;
using Microsoft.Extensions.Logging;
using Opus.Engine.Pal.Application;

namespace Garupan.Client.Windows.Bootstrap;

/// <summary>
/// Opens the host window. Order 100 — first stage in the "services" band, runs after
/// <see cref="ConfigurationStage"/> (Order 50) so the window adopts the player's
/// persisted resolution + vsync from <c>user://settings.gsav</c> rather than hard
/// defaults. The Settings screen marks those rows "restart required" because this stage
/// only consults the settings once, at boot.
/// </summary>
public sealed class EngineInitStage : IBootStage
{
    private const string WindowTitle = "STO";

    private readonly SettingsService _settings;
    private readonly ILogger<EngineInitStage> _logger;

    public EngineInitStage(SettingsService settings, ILogger<EngineInitStage> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public string Name => "EngineInit";

    public int Order => 100;

    public Task ExecuteAsync(BootContext ctx, CancellationToken ct)
    {
        var settings = _settings.Current;
        var opts = new WindowOptions(
            WindowTitle,
            settings.WindowWidth,
            settings.WindowHeight,
            Resizable: true,
            settings.VSync,
            WindowMode.Windowed);
        ctx.Window.Open(opts);
        _logger.LogInformation(
            "Engine.Pal.Windows window opened: {W}x{H}, vsync={Vsync}",
            ctx.Window.Size.Width,
            ctx.Window.Size.Height,
            opts.VSync);
        return Task.CompletedTask;
    }
}
