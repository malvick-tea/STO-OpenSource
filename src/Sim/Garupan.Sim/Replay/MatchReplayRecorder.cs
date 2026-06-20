using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Garupan.Sim.Snapshot;
using Microsoft.Extensions.Logging;
using Opus.Engine.Pal.Filesystem;
using Opus.Foundation;

namespace Garupan.Sim.Replay;

/// <summary>Authoritative match replay recorder. Holds an in-memory
/// <see cref="ReplayWriter"/> during the match, then flushes a single
/// HMAC-authenticated byte stream to <c>replays://match-{startTick}.grep</c>
/// on dispose. The replay bytes are produced by <see cref="ReplayWriter.Build"/>
/// which MACs the entire payload before any field is parsed — so a tampered
/// replay fails integrity verification on load, never reaching the
/// simulation replayer.
///
/// <para>
/// The recorder is intentionally synchronous on <see cref="RecordSnapshot"/>
/// (the in-memory append is cheap) and defers all disk I/O to
/// <see cref="FlushAsync"/>. The host's tick loop never blocks on the
/// filesystem; the flush runs on the post-match wind-down or on dispose,
/// whichever comes first.
/// </para>
/// </summary>
public sealed class MatchReplayRecorder : IReplaySink
{
    private const string ReplayFileExtension = ".grep";
    private readonly ReplayWriter _writer;
    private readonly IVfs _vfs;
    private readonly string _directory;
    private readonly byte[] _authenticationKey;
    private readonly long _startTick;
    private readonly ILogger _logger;
    private readonly object _sync = new();
    private bool _disposed;
    private bool _flushed;

    /// <param name="vfs">Virtual filesystem the recorder writes to. The
    /// recorder never touches raw disk paths; everything routes through
    /// <see cref="IVfs"/> so platform-specific containment checks
    /// (<c>PathContainment</c> on Windows, the loopback-rooted VFS in
    /// tests) apply uniformly.</param>
    /// <param name="directory">VFS directory under which the replay file
    /// is created. The recorder creates the directory if it does not
    /// exist; the directory must be a relative path under the VFS root or
    /// <see cref="IVfs"/> will reject it.</param>
    /// <param name="authenticationKey">HMAC key for the replay trailer.
    /// Should be an HKDF-derived sub-key scoped to the replay domain —
    /// never the raw install key. The recorder does not take ownership of
    /// this buffer; the caller is responsible for zeroing it.</param>
    /// <param name="tickRateHz">Sim tick rate the replay will be played
    /// back at. Mirrors <see cref="ReplayWriter"/>'s constructor.</param>
    /// <param name="startTick">First tick recorded. Mirrors
    /// <see cref="ReplayWriter"/>'s constructor.</param>
    public MatchReplayRecorder(
        IVfs vfs,
        string directory,
        ReadOnlyMemory<byte> authenticationKey,
        int tickRateHz,
        long startTick,
        ILogger<MatchReplayRecorder> logger)
    {
        ArgumentNullException.ThrowIfNull(vfs);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(logger);
        if (authenticationKey.Length < ReplayWire.MinimumAuthenticationKeyBytes)
        {
            throw new ArgumentException(
                $"Replay authentication key must contain at least {ReplayWire.MinimumAuthenticationKeyBytes} bytes.",
                nameof(authenticationKey));
        }

        _vfs = vfs;
        _directory = directory;
        _authenticationKey = authenticationKey.ToArray();
        _startTick = startTick;
        _logger = logger;
        _writer = new ReplayWriter(tickRateHz, new Tick(startTick));
    }

    /// <summary>Records one snapshot into the in-memory replay buffer.
    /// Thread-safe with respect to <see cref="FlushAsync"/>; the tick loop
    /// may call this from the sim thread while a shutdown path calls
    /// FlushAsync from the main thread.</summary>
    public void RecordSnapshot(WorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_sync)
        {
            if (_flushed || _disposed)
            {
                return;
            }

            _writer.RecordSnapshot(snapshot);
        }
    }

    /// <summary>Finalises the replay stream and writes it atomically to
    /// <c>{directory}/match-{startTick}.grep</c>. The file is written via
    /// <see cref="IVfs.WriteAllBytesAtomicAsync"/> so a crash mid-write
    /// never leaves a truncated replay on disk — the OS-level rename is
    /// the durability boundary.</summary>
    public async Task FlushAsync(CancellationToken ct)
    {
        byte[] bytes;
        lock (_sync)
        {
            if (_flushed || _disposed)
            {
                return;
            }

            _flushed = true;
            bytes = _writer.Build(_authenticationKey);
        }

        var path = $"{_directory}/match-{_startTick}{ReplayFileExtension}";
        try
        {
            await _vfs.WriteAllBytesAtomicAsync(path, bytes, ct).ConfigureAwait(false);
            _logger.LogInformation(
                "Match replay persisted: {Path} ({Bytes} bytes).",
                path,
                bytes.Length);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to persist match replay to {Path}.", path);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while persisting match replay to {Path}.", path);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // Best-effort flush on dispose. If the host already flushed (the
        // normal path on match-decided), this is a no-op. If the host is
        // tearing down mid-match, we lose the in-flight replay — acceptable
        // because the match never produced a verdict and the replay would
        // be incomplete anyway.
        try
        {
            FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Best-effort flush on dispose failed.");
        }

        CryptographicOperations.ZeroMemory(_authenticationKey);
    }
}
