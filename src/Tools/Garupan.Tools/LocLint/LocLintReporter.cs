using System;
using System.IO;

namespace Garupan.Tools.LocLint;

/// <summary>Renders a <see cref="LocLintReport"/> to a <see cref="TextWriter"/>. Pure
/// formatting — the command pipes <see cref="System.Console.Out"/> / <c>Error</c> in,
/// tests pipe a <see cref="StringWriter"/>. No exit-code logic here; the printer
/// never mutates the report.</summary>
internal sealed class LocLintReporter
{
    public void Render(LocLintReport report, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        output.WriteLine($"loc-lint: {report.ExpectedKeys.Count} referenced keys; {report.KeysByLocale.Count} locale catalogs loaded.");

        foreach (var (locale, available) in report.KeysByLocale)
        {
            var missing = report.MissingPerLocale[locale];
            var orphans = report.OrphanPerLocale[locale];
            output.WriteLine($"  [{locale}] {available.Count} keys ({missing.Count} missing, {orphans.Count} orphan)");
        }

        foreach (var missingLocale in report.MissingLocaleFiles)
        {
            error.WriteLine($"ERROR: locale file missing for '{missingLocale}'.");
        }

        foreach (var (locale, missing) in report.MissingPerLocale)
        {
            if (missing.Count == 0)
            {
                continue;
            }

            error.WriteLine($"ERROR: locale '{locale}' is missing {missing.Count} keys:");
            foreach (var key in missing)
            {
                error.WriteLine($"  - {key}");
            }
        }

        foreach (var (locale, orphans) in report.OrphanPerLocale)
        {
            if (orphans.Count == 0)
            {
                continue;
            }

            output.WriteLine($"WARN: locale '{locale}' has {orphans.Count} orphan keys (in CSV, never referenced):");
            foreach (var key in orphans)
            {
                output.WriteLine($"  - {key}");
            }
        }
    }
}
