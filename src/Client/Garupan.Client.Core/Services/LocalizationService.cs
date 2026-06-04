using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Opus.Engine.Pal.Filesystem;
using Opus.Foundation;
using Opus.Localisation;

namespace Garupan.Client.Core.Services;

/// <summary>
/// Owns the active <see cref="ITranslationCatalog"/> for the running app. Loads CSV
/// catalogues from <c>res://localization/{locale}.csv</c> via the host's <see cref="IVfs"/>.
/// Locale switching is synchronous after initial load — all catalogues live in memory.
/// </summary>
public sealed class LocalizationService
{
    private readonly IVfs _vfs;
    private readonly ILogger<LocalizationService> _logger;
    private readonly Dictionary<string, ITranslationCatalog> _catalogues = new(StringComparer.OrdinalIgnoreCase);
    private ITranslationCatalog _active = EmptyCatalog.Instance;

    public LocalizationService(IVfs vfs, ILogger<LocalizationService> logger)
    {
        _vfs = vfs;
        _logger = logger;
    }

    public event Action<ITranslationCatalog>? LocaleChanged;

    public ITranslationCatalog Active => _active;

    public IReadOnlyCollection<string> AvailableLocales => _catalogues.Keys;

    /// <summary>
    /// Loads every CSV under <c>res://localization/</c>. Hosts call this once during
    /// boot. <paramref name="locales"/> is the list of locale codes to try; missing
    /// files are logged and skipped (the locale just won't be available).
    /// </summary>
    public Task LoadAsync(IReadOnlyList<string> locales, CancellationToken ct)
    {
        Ensure.NotNull(locales);

        foreach (var locale in locales)
        {
            ct.ThrowIfCancellationRequested();
            var path = $"res://localization/{locale}.csv";
            if (!_vfs.Exists(path))
            {
                _logger.LogWarning("Locale '{Locale}' file missing at {Path}", locale, path);
                continue;
            }

            try
            {
                using var stream = _vfs.OpenRead(path);
                var catalog = CsvCatalog.ReadFrom(locale, stream);
                _catalogues[locale] = catalog;
                _logger.LogInformation("Loaded locale '{Locale}' ({Count} keys)", locale, catalog.AllKeys.Count);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Failed to load locale '{Locale}' from {Path}", locale, path);
            }
        }

        return Task.CompletedTask;
    }

    public void SetLocale(string locale)
    {
        Ensure.NotNullOrEmpty(locale);
        if (!_catalogues.TryGetValue(locale, out var cat))
        {
            _logger.LogWarning("SetLocale '{Locale}' — not loaded; staying on '{Active}'", locale, _active.Locale);
            return;
        }

        if (ReferenceEquals(cat, _active))
        {
            return;
        }

        _active = cat;
        LocaleChanged?.Invoke(cat);
        _logger.LogInformation("Active locale -> '{Locale}'", locale);
    }

    /// <summary>Convenience shortcut — equivalent to <c>Active.Get(key)</c>.</summary>
    public string T(TranslationKey key) => _active.Get(key);
}
