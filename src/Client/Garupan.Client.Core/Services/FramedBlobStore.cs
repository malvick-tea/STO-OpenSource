using System;
using System.IO;
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
    private readonly ILogger _logger;
    private readonly string _path;
    private readonly int _schemaVersion;
    private readonly string _label;

    public FramedBlobStore(
        IVfs vfs,
        IBinaryCodec codec,
        IClock clock,
        BuildInfo buildInfo,
        string path,
        int schemaVersion,
        string label,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(vfs);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(buildInfo);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        _vfs = vfs;
        _codec = codec;
        _clock = clock;
        _buildInfo = buildInfo;
        _logger = logger;
        _path = path;
        _schemaVersion = schemaVersion;
        _label = label;
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
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
            frame = ms.ToArray();
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "{Label}: failed to read {Path}; resetting.", _label, _path);
            return FramedLoadOutcome<TBody>.IoError();
        }

        var read = SaveHeaderSerializer.ReadFrame<TBody>(frame, _codec);
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

    public async Task SaveAsync(TBody body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var header = SaveHeader.Current(
                schemaVersion: _schemaVersion,
                appVersion: _buildInfo.Version,
                unixMs: _clock.UtcUnixMilliseconds());
            var frame = SaveHeaderSerializer.WriteFrame(header, body, _codec);
            await _vfs.WriteAllBytesAtomicAsync(_path, frame, ct).ConfigureAwait(false);
            _logger.LogDebug("{Label}: saved to {Path}.", _label, _path);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "{Label}: failed to persist {Path}.", _label, _path);
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
