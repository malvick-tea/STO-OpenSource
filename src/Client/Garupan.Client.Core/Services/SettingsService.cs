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
        ILogger<SettingsService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _store = new FramedBlobStore<AppSettings>(
            vfs, codec, clock, buildInfo,
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
    /// kicks off a save in the background. Use record <c>with</c>-expressions inside
    /// the lambda for ergonomic single-field updates.
    /// </summary>
    public void Apply(Func<AppSettings, AppSettings> mutate)
    {
        Ensure.NotNull(mutate);
        var next = mutate(_current);
        if (next == _current)
        {
            return;
        }

        _current = next;
        Changed?.Invoke(next);
        // Fire-and-forget save — failures already log; UI shouldn't block on disk writes.
        _ = _store.SaveAsync(_current, CancellationToken.None);
    }
}
