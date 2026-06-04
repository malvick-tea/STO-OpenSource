using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Garupan.Tools.Cli;

/// <summary>Routes the first CLI argument to the matching <see cref="ICommand"/> and
/// forwards the rest. Owns the <c>--help</c> banner so individual subcommands stay
/// focused on their own work.</summary>
internal sealed class CommandDispatcher
{
    private readonly Dictionary<string, ICommand> _commands;

    public CommandDispatcher(IEnumerable<ICommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _commands = commands.ToDictionary(c => c.Name, StringComparer.Ordinal);
    }

    public int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count == 0 || IsHelpFlag(args[0]))
        {
            WriteUsage(output);
            return args.Count == 0 ? CliExitCodes.Usage : CliExitCodes.Ok;
        }

        var commandName = args[0];
        if (!_commands.TryGetValue(commandName, out var command))
        {
            error.WriteLine($"Unknown subcommand '{commandName}'.");
            WriteUsage(error);
            return CliExitCodes.Usage;
        }

        var rest = new string[args.Count - 1];
        for (var i = 0; i < rest.Length; i++)
        {
            rest[i] = args[i + 1];
        }

        return command.Execute(rest, output, error);
    }

    private static bool IsHelpFlag(string arg) =>
        string.Equals(arg, "--help", StringComparison.Ordinal) ||
        string.Equals(arg, "-h", StringComparison.Ordinal) ||
        string.Equals(arg, "help", StringComparison.Ordinal);

    private void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("sto-tools - offline source checks");
        writer.WriteLine();
        writer.WriteLine("usage: sto-tools <subcommand> [args]");
        writer.WriteLine();
        writer.WriteLine("subcommands:");
        foreach (var (name, command) in _commands.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            writer.WriteLine($"  {name,-16} {command.Description}");
        }
    }
}
