using System.Threading;
using System.Threading.Tasks;
using Garupan.Client.Core.Bootstrap;
using Garupan.Content;
using Microsoft.Extensions.Logging;

namespace Garupan.Client.Windows.Bootstrap;

/// <summary>
/// Forces the canon crew CSV (player crew) to load + validate at boot. Order 126
/// — right after <see cref="CampaignDataStage"/> in the data-load band.
/// </summary>
public sealed class CrewDataStage : IBootStage
{
    private readonly CrewRoster _roster;
    private readonly ILogger<CrewDataStage> _logger;

    public CrewDataStage(CrewRoster roster, ILogger<CrewDataStage> logger)
    {
        _roster = roster;
        _logger = logger;
    }

    public string Name => "CrewData";

    public int Order => 126;

    public Task ExecuteAsync(BootContext ctx, CancellationToken ct)
    {
        _ = ctx;
        _ = ct;
        _logger.LogInformation(
            "Loaded crew roster for school '{School}' with {Members} members from CSV.",
            _roster.SchoolKey,
            _roster.All.Count);
        return Task.CompletedTask;
    }
}
