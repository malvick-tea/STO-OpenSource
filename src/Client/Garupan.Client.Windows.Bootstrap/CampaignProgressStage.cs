using System.Threading;
using System.Threading.Tasks;
using Garupan.Client.Core.Bootstrap;
using Garupan.Client.Core.Services;
using Microsoft.Extensions.Logging;

namespace Garupan.Client.Windows.Bootstrap;

/// <summary>
/// Loads <c>user://progress.json</c> into <see cref="CampaignProgressService"/>. Runs
/// after <see cref="CampaignDataStage"/> (125) and <see cref="CrewDataStage"/> (126)
/// so the campaign spec it's anchored to is already in memory; before
/// <see cref="AudioStage"/> (130) so progress is visible to anything that wants to
/// gate audio cues on completed missions later.
/// </summary>
public sealed class CampaignProgressStage : IBootStage
{
    private readonly CampaignProgressService _progress;
    private readonly ILogger<CampaignProgressStage> _logger;

    public CampaignProgressStage(CampaignProgressService progress, ILogger<CampaignProgressStage> logger)
    {
        _progress = progress;
        _logger = logger;
    }

    public string Name => "CampaignProgress";

    public int Order => 128;

    public async Task ExecuteAsync(BootContext ctx, CancellationToken ct)
    {
        _ = ctx;
        await _progress.LoadAsync(ct).ConfigureAwait(false);
        _logger.LogDebug(
            "Campaign progress ready: {Count} cleared.",
            _progress.Current.CompletedMissionIds.Count);
    }
}
