using System;
using System.Threading;
using System.Threading.Tasks;
using Opus.Persistence;

namespace Garupan.Client.Core.Services;

/// <summary>Fail-closed placeholder replaced by each platform composition module.</summary>
internal sealed class MissingSaveIntegrityKeyProvider : ISaveIntegrityKeyProvider
{
    public ValueTask<ReadOnlyMemory<byte>> GetKeyAsync(
        CancellationToken cancellationToken) =>
        ValueTask.FromException<ReadOnlyMemory<byte>>(
            new InvalidOperationException(
                "The platform did not register a protected save-integrity key provider."));
}
