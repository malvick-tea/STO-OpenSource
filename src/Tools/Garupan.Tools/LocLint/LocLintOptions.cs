using System;
using System.Collections.Generic;

namespace Garupan.Tools.LocLint;

/// <summary>Parsed command-line options for <see cref="LocLintCommand"/>. Lives apart
/// from the command so the parser is unit-testable without spinning up the rest of
/// the pipeline. Defaults assume the CLI is invoked from the repository root, but
/// every path is overridable via flag.</summary>
internal sealed record LocLintOptions(
    string LocaleDirectory,
    string DataDirectory,
    IReadOnlyList<string> Locales)
{
    public const string DefaultLocaleDirectory = "localization";
    public const string DefaultDataDirectory = "data";
    public static readonly IReadOnlyList<string> DefaultLocales = new[] { "en", "ru", "ja" };

    public static LocLintOptions Defaults() =>
        new(DefaultLocaleDirectory, DefaultDataDirectory, DefaultLocales);

    /// <summary>Parses <c>--locale-dir &lt;path&gt;</c>, <c>--data-dir &lt;path&gt;</c>,
    /// <c>--locales en,ru,ja</c>. Unknown flags throw <see cref="ArgumentException"/>
    /// so the caller can render usage; missing values throw the same way.</summary>
    public static LocLintOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var localeDir = DefaultLocaleDirectory;
        var dataDir = DefaultDataDirectory;
        var locales = DefaultLocales;

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--locale-dir":
                    localeDir = RequireValue(args, ref i);
                    break;
                case "--data-dir":
                    dataDir = RequireValue(args, ref i);
                    break;
                case "--locales":
                    locales = ParseLocaleList(RequireValue(args, ref i));
                    break;
                default:
                    throw new ArgumentException($"Unknown loc-lint argument '{args[i]}'.");
            }
        }

        return new LocLintOptions(localeDir, dataDir, locales);
    }

    private static string RequireValue(IReadOnlyList<string> args, ref int i)
    {
        if (i + 1 >= args.Count)
        {
            throw new ArgumentException($"Argument '{args[i]}' requires a value.");
        }

        i++;
        return args[i];
    }

    private static IReadOnlyList<string> ParseLocaleList(string raw)
    {
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            throw new ArgumentException("--locales requires at least one locale code.");
        }

        return parts;
    }
}
