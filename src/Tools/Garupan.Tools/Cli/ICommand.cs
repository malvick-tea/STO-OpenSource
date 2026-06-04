using System.Collections.Generic;
using System.IO;

namespace Garupan.Tools.Cli;

/// <summary>One subcommand of the <c>sto-tools</c> CLI. Each impl owns its own
/// argument parsing — the dispatcher only chooses which command to invoke.</summary>
internal interface ICommand
{
    /// <summary>Subcommand keyword used as the first CLI argument (e.g. <c>loc-lint</c>).</summary>
    string Name { get; }

    /// <summary>One-line help summary listed by the top-level <c>--help</c>.</summary>
    string Description { get; }

    /// <summary>Runs the command with the remainder of the args (after the keyword).
    /// Writes structured output through the supplied writers so tests don't have to
    /// capture <see cref="System.Console"/>. Returns a <see cref="CliExitCodes"/> value.</summary>
    int Execute(IReadOnlyList<string> args, TextWriter output, TextWriter error);
}
