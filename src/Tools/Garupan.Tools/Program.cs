using System;
using Garupan.Tools.Cli;
using Garupan.Tools.ContentLint;
using Garupan.Tools.LocLint;

namespace Garupan.Tools;

/// <summary>Entry point for the <c>sto-tools</c> CLI. Builds the dispatcher with
/// every available subcommand and forwards to the appropriate one. Keep this file
/// short — every subcommand owns its own argument parsing.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var dispatcher = new CommandDispatcher(new ICommand[]
        {
            new LocLintCommand(),
            new ContentLintCommand(),
        });

        return dispatcher.Run(args, Console.Out, Console.Error);
    }
}
