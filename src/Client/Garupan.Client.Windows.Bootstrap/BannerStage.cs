using System.Threading;
using System.Threading.Tasks;
using Garupan.Client.Core.Bootstrap;
using Microsoft.Extensions.Logging;
using Opus.Engine.Pal.Time;
using Opus.Foundation;

namespace Garupan.Client.Windows.Bootstrap;

/// <summary>
/// Final-band stage. Logs the boot duration and a build banner. The banner line mirrors
/// the C++ <c>svo::engine::print_banner</c> output: project + version + configuration +
/// runtime + OS / arch, on a single line so a crash dump or log archive carries an
/// unambiguous signature of the build that produced it.
/// </summary>
public sealed class BannerStage : IBootStage
{
    private readonly IHighResClock _clock;
    private readonly ILogger<BannerStage> _logger;

    public BannerStage(IHighResClock clock, ILogger<BannerStage> logger)
    {
        _clock = clock;
        _logger = logger;
    }

    public string Name => "Banner";

    public int Order => 1000;

    public Task ExecuteAsync(BootContext ctx, CancellationToken ct)
    {
        _logger.LogInformation("{Banner}", BuildInfo.Current.ToBannerLine());
        _logger.LogInformation(
            "Boot complete @ t={T:0.000}s. Entering frame loop.",
            _clock.GetElapsedSeconds());
        return Task.CompletedTask;
    }
}
