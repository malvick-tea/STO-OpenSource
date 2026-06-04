using System.Threading;
using System.Threading.Tasks;
using Garupan.Client.Core.Bootstrap;
using Garupan.Content;
using Microsoft.Extensions.Logging;

namespace Garupan.Client.Windows.Bootstrap;

/// <summary>
/// Forces the match-mode CSV (local test line-up: Hungry Battles 10v10 + Tactical 5v5)
/// to load + validate at boot. Order 127 — right after <see cref="CrewDataStage"/> in
/// the data-load band so a malformed row surfaces as a stage failure on the friendly
/// error screen instead of crashing on the first PLAY click.
/// </summary>
public sealed class MatchModeDataStage : IBootStage
{
    private readonly MatchModeCatalog _catalog;
    private readonly ILogger<MatchModeDataStage> _logger;

    public MatchModeDataStage(MatchModeCatalog catalog, ILogger<MatchModeDataStage> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public string Name => "MatchModeData";

    public int Order => 127;

    public Task ExecuteAsync(BootContext ctx, CancellationToken ct)
    {
        _ = ctx;
        _ = ct;
        _logger.LogInformation(
            "Loaded {Count} match mode(s) from CSV.", _catalog.Count);
        return Task.CompletedTask;
    }
}
