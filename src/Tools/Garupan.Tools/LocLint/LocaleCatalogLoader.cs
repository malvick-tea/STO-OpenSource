using System;
using System.Collections.Generic;
using System.IO;
using Opus.Localisation;

namespace Garupan.Tools.LocLint;

/// <summary>Loads one CSV per requested locale from <paramref name="localeDirectory"/>
/// using <see cref="CsvCatalog.ReadFrom"/> — the same parser the runtime uses. The
/// loader stays additive: a missing locale file is reported separately rather than
/// failing the whole pass, so a fresh translator can iterate on one language without
/// the lint exploding.</summary>
internal sealed class LocaleCatalogLoader
{
    public LocaleCatalogLoadResult Load(string localeDirectory, IReadOnlyList<string> locales)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localeDirectory);
        ArgumentNullException.ThrowIfNull(locales);

        var keysByLocale = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        var missingFiles = new List<string>();
        foreach (var locale in locales)
        {
            var path = Path.Combine(localeDirectory, $"{locale}.csv");
            if (!File.Exists(path))
            {
                missingFiles.Add(locale);
                continue;
            }

            using var stream = File.OpenRead(path);
            var catalog = CsvCatalog.ReadFrom(locale, stream);
            keysByLocale[locale] = catalog.AllKeys;
        }

        return new LocaleCatalogLoadResult(keysByLocale, missingFiles);
    }
}

/// <summary>Outcome of a <see cref="LocaleCatalogLoader.Load"/> call. <see cref="MissingLocaleFiles"/>
/// surfaces locales that were requested but had no CSV on disk; the lint reports them
/// separately from key-coverage errors.</summary>
internal sealed record LocaleCatalogLoadResult(
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> KeysByLocale,
    IReadOnlyList<string> MissingLocaleFiles);
