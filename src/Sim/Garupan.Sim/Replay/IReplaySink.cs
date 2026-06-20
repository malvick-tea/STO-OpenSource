using System;
using System.Threading;
using System.Threading.Tasks;
using Garupan.Sim.Snapshot;

namespace Garupan.Sim.Replay;

/// <summary>Sink interface for authoritative match replays. Decouples
/// <c>MatchHost</c> from the concrete <see cref="ReplayWriter"/> + persistence
/// path so the host can record every snapshot it broadcasts without taking a
/// direct dependency on a file system. Implementations are responsible for
/// durability (atomic write, HMAC trailer via <see cref="ReplayWriter.Build"/>,
/// and post-match flush).</summary>
public interface IReplaySink : IDisposable
{
    /// <summary>Records one snapshot produced by the authoritative sim.
    /// Called inside the match tick loop, so the implementation must be
    /// non-blocking on the calling thread — any disk I/O belongs in a
    /// background task. Idempotent on duplicate ticks: a second call with
    /// the same <see cref="WorldSnapshot.Tick"/> must be a no-op rather
    /// than throw, so the broadcast path can re-record a re-broadcast
    /// snapshot without tearing down the match.</summary>
    void RecordSnapshot(WorldSnapshot snapshot);

    /// <summary>Finalises the replay stream and persists it durably. Called
    /// once when the match is decided (or when the host is disposing). The
    /// returned task completes when the replay bytes are durable on disk
    /// (or when an error has been logged). Shutdown paths should await this
    /// so a fast exit cannot truncate the in-flight write.</summary>
    Task FlushAsync(CancellationToken ct);
}

/// <summary>No-op sink used when match replay recording is disabled. The
/// host holds this rather than a nullable <see cref="IReplaySink"/> so the
/// tick loop has no null-check on the hot path.</summary>
public sealed class NullReplaySink : IReplaySink
{
    private NullReplaySink()
    {
    }

    public static NullReplaySink Instance { get; } = new();

    public void RecordSnapshot(WorldSnapshot snapshot)
    {
        // Intentionally empty — recording is disabled.
    }

    public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;

    public void Dispose()
    {
        // Nothing to dispose.
    }
}
