namespace Garupan.Tools.Cli;

/// <summary>Stable exit-code surface for every subcommand. Wrapper scripts and CI
/// gates branch on these — keeping them as named constants makes a green/red read at
/// the call site instead of a magic-int audit.</summary>
internal static class CliExitCodes
{
    /// <summary>Subcommand ran clean. The pre-alpha tools treat "warnings only" as Ok too —
    /// failures bump to <see cref="Failure"/>.</summary>
    public const int Ok = 0;

    /// <summary>Subcommand ran and found problems. Loc-lint with missing keys, content-lint
    /// with parse errors. Logged details preceded the exit.</summary>
    public const int Failure = 1;

    /// <summary>The CLI itself was invoked wrong — unknown subcommand, missing required
    /// argument, etc. Usage text is printed to stderr.</summary>
    public const int Usage = 2;
}
