using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Opus.Engine.Pal.Filesystem;
using Opus.Foundation;
using Opus.Persistence;

namespace Garupan.Client.Core.Services;

/// <summary>One-file binary blob anchored at a fixed <see cref="IVfs"/> path. Owns the
/// shared framing — <see cref="SaveHeaderSerializer"/> + <see cref="IBinaryCodec"/>
/// stamped with the running <see cref="BuildInfo"/> and <see cref="IClock"/> — so
/// service classes (<see cref="SettingsService"/>, <see cref="CampaignProgressService"/>,
/// future per-user blobs) carry only their domain logic.</summary>
/// <remarks>
/// Extracted in Phase 14 quality pass: both pre-extraction services repeated the same
/// six steps (Exists → OpenRead → CopyTo → ReadFrame → schema check → assign) and the
/// same three saves (build header → WriteFrame → WriteAllBytesAtomicAsync). One bug
/// in the recipe was one bug in two places.
/// </remarks>
internal sealed class FramedBlobStore<TBody>
    where TBody : class
{
    private readonly IVfs _vfs;
    private readonly IBinaryCodec _codec;
    private readonly IClock _clock;
    private readonly BuildInfo _buildInfo;
    private readonly ISaveIntegrityKeyProvider _integrityKeyProvider;
    private readonly ILogger _logger;
    private readonly string _path;
    private readonly int _schemaVersion;
    private readonly string _label;
    private readonly byte[] _domain;
    private readonly object _saveLock = new();
    private Task _saveTail = Task.CompletedTask;

    public FramedBlobStore(
        IVfs vfs,
        IBinaryCodec codec,
        IClock clock,
        BuildInfo buildInfo,
        ISaveIntegrityKeyProvider integrityKeyProvider,
        string path,
        int schemaVersion,
        string label,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(vfs);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(buildInfo);
        ArgumentNullException.ThrowIfNull(integrityKeyProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        _vfs = vfs;
        _codec = codec;
        _clock = clock;
        _buildInfo = buildInfo;
        _integrityKeyProvider = integrityKeyProvider;
        _logger = logger;
        _path = path;
        _schemaVersion = schemaVersion;
        _label = label;
        // Domain separation: each blob type derives an independent MAC key
        // from the shared install key, so a tampered settings frame cannot be
        // replayed as a progress frame (or vice versa) even if the install
        // key is identical. Schema version is folded in so a v1 frame cannot
        // be replayed against a v2 schema after a migration.
        _domain = System.Text.Encoding.UTF8.GetBytes($"Opus.Save.{label}.v{schemaVersion}");
    }

    public string Path => _path;

    public async Task<FramedLoadOutcome<TBody>> TryLoadAsync(CancellationToken ct)
    {
        if (!_vfs.Exists(_path))
        {
            _logger.LogInformation("{Label}: no file at {Path}; starting fresh.", _label, _path);
            return FramedLoadOutcome<TBody>.NoFile();
        }

        byte[] frame;
        try
        {
            using var stream = _vfs.OpenRead(_path);
            frame = await ReadBoundedFrameAsync(stream, ct).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "{Label}: failed to read {Path}; resetting.", _label, _path);
            return FramedLoadOutcome<TBody>.IoError();
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "{Label}: oversized frame at {Path}; resetting.", _label, _path);
            return FramedLoadOutcome<TBody>.Corrupt();
        }

        ReadOnlyMemory<byte> authenticationKey;
        try
        {
            authenticationKey = await _integrityKeyProvider.GetKeyAsync(ct).ConfigureAwait(false);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            // DPAPI can throw when the user profile is corrupt, when the
            // wrapped key blob is transferred across users, or when roaming
            // profile replication truncated the file. The save path already
            // catches this in SaveCoreAsync; mirroring the behaviour here
            // keeps the load path resilient — the player sees a fresh empty
            // blob instead of a boot-time crash they cannot recover from.
            _logger.LogCritical(
                ex,
                "{Label}: integrity-key failure while loading {Path}; resetting.",
                _label,
                _path);
            return FramedLoadOutcome<TBody>.Corrupt();
        }

        var read = SaveHeaderSerializer.ReadFrame<TBody>(
            frame,
            _codec,
            authenticationKey.Span,
            _domain);
        if (read.IsErr)
        {
            _logger.LogWarning(
                "{Label}: corrupt frame at {Path} ({Code}); resetting.",
                _label, _path, read.UnwrapErr().Code);
            return FramedLoadOutcome<TBody>.Corrupt();
        }

        var (header, body) = read.Unwrap();
        if (header.SchemaVersion != _schemaVersion)
        {
            _logger.LogWarning(
                "{Label}: schema {Found} != current {Expected}; resetting until migrators exist.",
                _label, header.SchemaVersion, _schemaVersion);
            return FramedLoadOutcome<TBody>.SchemaMismatch();
        }

        return FramedLoadOutcome<TBody>.Loaded(body);
    }

    public Task SaveAsync(TBody body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        lock (_saveLock)
        {
            _saveTail = _saveTail.ContinueWith(
                    _ => SaveCoreAsync(body, ct),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default)
                .Unwrap();
            return _saveTail;
        }
    }

    private async Task SaveCoreAsync(TBody body, CancellationToken ct)
    {
        try
        {
            var header = SaveHeader.Current(
                schemaVersion: _schemaVersion,
                appVersion: _buildInfo.Version,
                unixMs: _clock.UtcUnixMilliseconds());
            var authenticationKey = await _integrityKeyProvider.GetKeyAsync(ct).ConfigureAwait(false);
            var frame = SaveHeaderSerializer.WriteFrame(
                header,
                body,
                _codec,
                authenticationKey.Span,
                _domain);
            await _vfs.WriteAllBytesAtomicAsync(_path, frame, ct).ConfigureAwait(false);
            _logger.LogDebug("{Label}: saved to {Path}.", _label, _path);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "{Label}: failed to persist {Path}.", _label, _path);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "{Label}: access denied while persisting {Path}.", _label, _path);
        }
        catch (CryptographicException ex)
        {
            _logger.LogCritical(ex, "{Label}: integrity-key failure while persisting {Path}.", _label, _path);
        }
    }

    private static async Task<byte[]> ReadBoundedFrameAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        if (stream.CanSeek && stream.Length > SaveHeaderSerializer.MaxFrameBytes)
        {
            throw new InvalidDataException(
                $"Save frame exceeds the {SaveHeaderSerializer.MaxFrameBytes}-byte limit.");
        }

        var initialCapacity = stream.CanSeek ? checked((int)stream.Length) : 0;
        using var output = new MemoryStream(initialCapacity);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return output.ToArray();
                }

                if (output.Length + read > SaveHeaderSerializer.MaxFrameBytes)
                {
                    throw new InvalidDataException(
                        $"Save frame exceeds the {SaveHeaderSerializer.MaxFrameBytes}-byte limit.");
                }

                output.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }
}

/// <summary>Outcome of <see cref="FramedBlobStore{TBody}.TryLoadAsync"/>. <see cref="Body"/>
/// is non-null only when <see cref="Status"/> is <see cref="FramedLoadStatus.Loaded"/>.
/// Status codes distinguish "fresh start, expected" from "broken file, alarming".</summary>
internal sealed record FramedLoadOutcome<TBody>(FramedLoadStatus Status, TBody? Body)
    where TBody : class
{
    public bool IsLoaded => Status == FramedLoadStatus.Loaded && Body is not null;

    public static FramedLoadOutcome<TBody> Loaded(TBody body) => new(FramedLoadStatus.Loaded, body);

    public static FramedLoadOutcome<TBody> NoFile() => new(FramedLoadStatus.NoFile, null);

    public static FramedLoadOutcome<TBody> IoError() => new(FramedLoadStatus.IoError, null);

    public static FramedLoadOutcome<TBody> Corrupt() => new(FramedLoadStatus.Corrupt, null);

    public static FramedLoadOutcome<TBody> SchemaMismatch() => new(FramedLoadStatus.SchemaMismatch, null);
}

internal enum FramedLoadStatus
{
    Loaded,
    NoFile,
    IoError,
    Corrupt,
    SchemaMismatch,
}
