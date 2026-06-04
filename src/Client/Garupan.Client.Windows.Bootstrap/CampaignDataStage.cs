using System.Threading;
using System.Threading.Tasks;
using Garupan.Client.Core.Bootstrap;
using Garupan.Content;
using Microsoft.Extensions.Logging;

namespace Garupan.Client.Windows.Bootstrap;

/// <summary>
/// Forces the canon campaign CSV to load + validate at boot rather than the first
/// CAMPAIGN click. Resolves the DI singleton; any malformed row / dangling
/// prerequisite surfaces here as a stage failure that the host can present as a
/// friendly error screen rather than a mid-game crash.
/// </summary>
/// <remarks>
/// Order 125 — between Localization (120) and Audio (130), in the data-load band.
/// The actual <see cref="CampaignSpec"/> factory is registered in
/// <c>WindowsServicesModule</c>; this stage exists solely to materialise it.
/// </remarks>
public sealed class CampaignDataStage : IBootStage
{
    private readonly CampaignSpec _campaign;
    private readonly ILogger<CampaignDataStage> _logger;

    public CampaignDataStage(CampaignSpec campaign, ILogger<CampaignDataStage> logger)
    {
        _campaign = campaign;
        _logger = logger;
    }

    public string Name => "CampaignData";

    public int Order => 125;

    public Task ExecuteAsync(BootContext ctx, CancellationToken ct)
    {
        _ = ctx;
        _ = ct;
        _logger.LogInformation(
            "Loaded campaign '{Id}' with {Missions} missions from CSV.",
            _campaign.Id,
            _campaign.Missions.Count);
        return Task.CompletedTask;
    }
}
