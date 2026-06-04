using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Opus.Foundation;

namespace Garupan.Client.Core.Bootstrap;

/// <summary>
/// Runs registered <see cref="IBootStage"/>s in <see cref="IBootStage.Order"/> ascending.
/// Failures abort the sequence and surface as <see cref="BootFailureException"/> for the
/// splash / error screen to render a user-visible message.
/// </summary>
public sealed class BootSequence
{
    private readonly IBootStage[] _stages;
    private readonly ILogger<BootSequence> _logger;

    public BootSequence(IEnumerable<IBootStage> stages, ILogger<BootSequence> logger)
    {
        Ensure.NotNull(stages);
        Ensure.NotNull(logger);
        _stages = stages.OrderBy(s => s.Order).ToArray();
        _logger = logger;
    }

    public event Action<IBootStage>? StageStarted;

    public event Action<IBootStage, TimeSpan>? StageCompleted;

    public event Action<IBootStage, Exception>? StageFailed;

    public IReadOnlyList<IBootStage> Stages => _stages;

    public async Task RunAsync(BootContext ctx, CancellationToken ct)
    {
        Ensure.NotNull(ctx);

        _logger.LogInformation("Boot sequence starting — {Count} stages.", _stages.Length);
        var totalSw = Stopwatch.StartNew();

        foreach (var stage in _stages)
        {
            ct.ThrowIfCancellationRequested();
            StageStarted?.Invoke(stage);
            _logger.LogInformation("→ stage [{Order:000}] {Name}", stage.Order, stage.Name);

            var sw = Stopwatch.StartNew();
            try
            {
                await stage.ExecuteAsync(ctx, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Boot cancelled during {Stage}", stage.Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stage {Stage} failed.", stage.Name);
                StageFailed?.Invoke(stage, ex);
                throw new BootFailureException(stage, ex);
            }

            sw.Stop();
            StageCompleted?.Invoke(stage, sw.Elapsed);
            _logger.LogInformation("✓ stage {Name} done in {Ms} ms", stage.Name, sw.ElapsedMilliseconds);
        }

        totalSw.Stop();
        _logger.LogInformation("Boot finished in {Ms} ms", totalSw.ElapsedMilliseconds);
    }
}
