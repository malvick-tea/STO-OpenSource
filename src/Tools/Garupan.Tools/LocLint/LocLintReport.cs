using System;
using System.Collections.Generic;
using System.Linq;

namespace Garupan.Tools.LocLint;

/// <summary>Pure diff of "expected" translation keys (from the registry + data refs)
/// against what each locale catalog actually carries. Splitting the diff out of the
/// command means the printer and the exit-code logic stay testable without spinning a
/// CLI process.</summary>
internal sealed record LocLintReport(
    IReadOnlyCollection<string> ExpectedKeys,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> KeysByLocale,
    IReadOnlyList<string> MissingLocaleFiles,
    IReadOnlyDictionary<string, IReadOnlyList<string>> MissingPerLocale,
    IReadOnlyDictionary<string, IReadOnlyList<string>> OrphanPerLocale)
{
    /// <summary>True when at least one locale is missing a referenced key, or its CSV
    /// file is absent. Orphan keys (in CSV but not referenced) are reported but DO NOT
    /// flip the gate — translators can stage copy ahead of code without breaking CI.</summary>
    public bool HasFailures =>
        MissingLocaleFiles.Count > 0 ||
        MissingPerLocale.Values.Any(list => list.Count > 0);

    public static LocLintReport Build(
        IReadOnlyCollection<string> expectedKeys,
        LocaleCatalogLoadResult locales)
    {
        ArgumentNullException.ThrowIfNull(expectedKeys);
        ArgumentNullException.ThrowIfNull(locales);

        var expectedSet = new HashSet<string>(expectedKeys, StringComparer.Ordinal);
        var missingPerLocale = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var orphanPerLocale = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var (locale, available) in locales.KeysByLocale)
        {
            var availableSet = new HashSet<string>(available, StringComparer.Ordinal);
            missingPerLocale[locale] = expectedSet.Where(k => !availableSet.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToArray();
            orphanPerLocale[locale] = availableSet.Where(k => !expectedSet.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        }

        return new LocLintReport(
            ExpectedKeys: expectedSet,
            KeysByLocale: locales.KeysByLocale,
            MissingLocaleFiles: locales.MissingLocaleFiles,
            MissingPerLocale: missingPerLocale,
            OrphanPerLocale: orphanPerLocale);
    }
}
