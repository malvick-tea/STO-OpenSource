using System;
using System.Collections.Generic;

namespace Garupan.Tools.ContentLint;

/// <summary>Parsed command-line options for <see cref="ContentLintCommand"/>. Mirrors
/// the loc-lint shape so a future <c>--all</c> wrapper can call both subcommands with
/// identical arguments. Today there is only the data directory.</summary>
internal sealed record ContentLintOptions(string DataDirectory)
{
    public const string DefaultDataDirectory = "data";

    public static ContentLintOptions Defaults() => new(DefaultDataDirectory);

    public static ContentLintOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var dataDir = DefaultDataDirectory;

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--data-dir":
                    dataDir = RequireValue(args, ref i);
                    break;
                default:
                    throw new ArgumentException($"Unknown content-lint argument '{args[i]}'.");
            }
        }

        return new ContentLintOptions(dataDir);
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
}
