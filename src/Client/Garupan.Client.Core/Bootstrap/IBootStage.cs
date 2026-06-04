using System.Threading;
using System.Threading.Tasks;

namespace Garupan.Client.Core.Bootstrap;

/// <summary>
/// One ordered step of the boot sequence. Stages are resolved from DI and ordered
/// by their <see cref="Order"/>. Each stage MUST be idempotent and MUST NOT mutate
/// global state on failure (so a retry can re-run cleanly).
/// </summary>
/// <remarks>
/// Order bands (reserved):
///   <c>0–99</c>     — infra (config, logging, telemetry pre-init)
///   <c>100–199</c>  — services (engine init, content, localisation, audio, input)
///   <c>200–299</c>  — domain (session, save-load, network handshake)
///   <c>1000+</c>    — UI / scene (initial screen replacement)
/// </remarks>
public interface IBootStage
{
    /// <summary>Stable display name for logs / splash UI.</summary>
    string Name { get; }

    int Order { get; }

    Task ExecuteAsync(BootContext ctx, CancellationToken ct);
}
