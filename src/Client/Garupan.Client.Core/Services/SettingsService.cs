using System;
using System.Threading;
using System.Threading.Tasks;
using Garupan.Client.Core.Application;
using Microsoft.Extensions.Logging;
using Opus.Engine.Pal.Filesystem;
using Opus.Foundation;
using Opus.Persistence;

namespace Garupan.Client.Core.Services;

/// <summary>
/// Owns the in-memory <see cref="AppSettings"/> snapshot, loads it from
/// <c>user://settings.gsav</c> via <see cref="FramedBlobStore{TBody}"/>, persists
/// changes back. Settings mutate via <see cref="Apply"/> so subscribers (renderer,
/// audio busses, locale) get a single notification per coherent change, not one per
/// field.
/// </summary>
/// <remarks>
/// All blob framing (header, codec, schema check, atomic write) lives in
/// <see cref="FramedBlobStore{TBody}"/>; this class focuses on the in-memory snapshot,
/// the Changed event, and copy-with mutation semantics.
/// </remarks>
public sealed class SettingsService
{
    /// <summary>VFS path of the persisted binary frame. The <c>.gsav</c> extension
    /// disambiguates from any legacy <c>settings.json</c> a stray dev box might still
    /// carry; nothing reads the JSON path any more.</summary>
    public const string SettingsPath = "user://settings.gsav";

    private readonly FramedBlobStore<AppSettings> _store;
    private readonly ILogger<SettingsService> _logger;
    private AppSettings _current = AppSettings.Default;

    public SettingsService(
        IVfs vfs,
        IBinaryCodec codec,
        IClock clock,
        BuildInfo buildInfo,
        ISaveIntegrityKeyProvider integrityKeyProvider,
        ILogger<SettingsService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _store = new FramedBlobStore<AppSettings>(
            vfs, codec, clock, buildInfo, integrityKeyProvider,
            SettingsPath, SaveSchemas.Settings, "settings", logger);
    }

    public event Action<AppSettings>? Changed;

    public AppSettings Current => _current;

    public async Task LoadAsync(CancellationToken ct)
    {
        var outcome = await _store.TryLoadAsync(ct).ConfigureAwait(false);
        if (outcome.IsLoaded)
        {
            _current = outcome.Body!;
            _logger.LogInformation(
                "Settings loaded: locale={Locale}, {W}x{H}, vsync={VSync}",
                _current.Locale,
                _current.WindowWidth,
                _current.WindowHeight,
                _current.VSync);
            return;
        }

        _current = AppSettings.Default;
        if (outcome.Status == FramedLoadStatus.NoFile)
        {
            await _store.SaveAsync(_current, ct).ConfigureAwait(false);
        }
    }

    public Task SaveAsync(CancellationToken ct) => _store.SaveAsync(_current, ct);

    /// <summary>
    /// Mutates the settings via a copy-with lambda, raises <see cref="Changed"/>, and
    /// kicks off a save. Use record <c>with</c>-expressions inside the lambda for
    /// ergonomic single-field updates.
    /// </summary>
    /// <remarks>This overload exists for synchronous UI handlers that already
    /// suppress CS4014. The save task is tracked through the
    /// <see cref="FramedBlobStore{TBody}"/> tail so an unobserved exception still
    /// surfaces in the log; the only thing the caller loses by not awaiting is
    /// the ability to know when disk I/O finished. Shutdown paths should call
    /// <see cref="SaveAsync"/> explicitly so a fast exit cannot truncate the
    /// in-flight save.</remarks>
    public void Apply(Func<AppSettings, AppSettings> mutate)
        => Apply(mutate, CancellationToken.None);

    /// <summary>
    /// Mutates the settings via a copy-with lambda, raises <see cref="Changed"/>, and
    /// kicks off a save. Use record <c>with</c>-expressions inside the lambda for
    /// ergonomic single-field updates.
    /// </summary>
    /// <remarks>The returned <see cref="Task"/> completes when the framed blob has
    /// been written to disk (or when an IOException / UnauthorizedAccessException /
    /// CryptographicException has been logged). Callers that fire user-initiated
    /// mutations should await it on shutdown so a fast exit cannot truncate the
    /// in-flight save. Synchronous UI handlers may use the single-argument
    /// overload; the underlying <see cref="FramedBlobStore{TBody}"/> serialises
    /// every save through a single tail task so out-of-order completions are
    /// still impossible.</remarks>
    public Task Apply(Func<AppSettings, AppSettings> mutate, CancellationToken ct)
    {
        Ensure.NotNull(mutate);
        var next = mutate(_current);
        if (next == _current)
        {
            return Task.CompletedTask;
        }

        _current = next;
        Changed?.Invoke(next);
        return _store.SaveAsync(_current, ct);
    }
}
