using System;
using System.IO;
using System.Net.Sockets;
using Garupan.Content;
using Microsoft.Extensions.Logging;

namespace Garupan.Server.Console;

/// <summary>
/// The testable surface behind <see cref="Program.Main"/>. Accepts the raw CLI args + an
/// already-configured logger factory + an already-installed <see cref="ShutdownSignal"/>
/// so each piece can be swapped in tests, and returns the process exit code.
/// </summary>
/// <remarks>
/// Exit codes:
/// <list type="table">
/// <listheader><term>code</term><description>meaning</description></listheader>
/// <item><term>0</term><description>clean shutdown</description></item>
/// <item><term>1</term><description>help requested — printed and exited (treated as a clean stop)</description></item>
/// <item><term>2</term><description>option parse error — diagnostic written to stderr</description></item>
/// <item><term>3</term><description>UDP bind failed — port likely in use; diagnostic on stderr</description></item>
/// <item><term>4</term><description>match-mode catalog load / lookup failed</description></item>
/// </list>
/// </remarks>
public static class ServerConsoleEntry
{
    public const int ExitOk = 0;
    public const int ExitHelp = 1;
    public const int ExitOptionsError = 2;
    public const int ExitBindError = 3;
    public const int ExitCatalogError = 4;

    /// <summary>Relative path the bundled match-mode CSV lives at — copied to the exe
    /// output directory by the csproj. Tests override this through the alternate Run
    /// overload that takes a pre-loaded catalogue.</summary>
    public const string MatchModeCsvRelativePath = "content/match-modes.csv";

    public static int Run(string[] args, ILoggerFactory loggerFactory, ShutdownSignal shutdownSignal)
    {
        var catalog = LoadCatalogOrNull(loggerFactory);
        if (catalog is null)
        {
            return ExitCatalogError;
        }

        return Run(args, catalog, loggerFactory, shutdownSignal);
    }

    /// <summary>Test-friendly overload: skip the on-disk CSV load and use the supplied
    /// in-memory <paramref name="catalog"/>. Runtime callers go through the parameter-
    /// less <see cref="Run(string[],ILoggerFactory,ShutdownSignal)"/> which loads from
    /// <see cref="MatchModeCsvRelativePath"/>.</summary>
    public static int Run(
        string[] args,
        MatchModeCatalog catalog,
        ILoggerFactory loggerFactory,
        ShutdownSignal shutdownSignal)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(shutdownSignal);

        var parsed = ServerConsoleOptionsParser.Parse(args);
        switch (parsed)
        {
            case ServerConsoleOptionsParser.ParseResult.HelpRequested:
                System.Console.Out.WriteLine(ServerConsoleOptionsParser.HelpText);
                return ExitHelp;
            case ServerConsoleOptionsParser.ParseResult.Failed(var diagnostic):
                System.Console.Error.WriteLine($"sto-server: {diagnostic}");
                System.Console.Error.WriteLine("Run with --help for usage.");
                return ExitOptionsError;
        }

        var options = ((ServerConsoleOptionsParser.ParseResult.Ok)parsed).Options;
        var logger = loggerFactory.CreateLogger("STO.Server.Console");

        var mode = catalog.Find(options.MatchModeId);
        if (mode is null)
        {
            var known = string.Join(", ", IdsOf(catalog));
            System.Console.Error.WriteLine(
                $"sto-server: unknown match mode id '{options.MatchModeId}'. Known modes: {known}.");
            return ExitCatalogError;
        }

        ServerHostBundle bundle;
        try
        {
            bundle = ServerHostBundle.Create(options, mode, loggerFactory);
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                // The overwhelmingly common cause: a previous server is still running on
                // this port (closing the window without Ctrl+C leaves the process bound).
                // Surface it as one actionable line, NOT a SocketException stack dump that
                // reads like a crash.
                logger.LogWarning(
                    "UDP {Endpoint} is already in use — another server is probably still running.",
                    options.ListenEndpoint);
                System.Console.Error.WriteLine(
                    $"sto-server: {options.ListenEndpoint} is already in use — another server is probably " +
                    "running. Close it (or start this one on a free port with --port <n>), then retry.");
            }
            else
            {
                logger.LogError(ex, "UDP bind failed on {Endpoint}", options.ListenEndpoint);
                System.Console.Error.WriteLine($"sto-server: UDP bind on {options.ListenEndpoint} failed: {ex.Message}");
            }

            return ExitBindError;
        }

        using (bundle)
        {
            logger.LogInformation("Server up. Press Ctrl+C to shut down.");
            bundle.TickLoop.Run(shutdownSignal.Token);
            logger.LogInformation("Shutdown signal observed; exiting.");
        }

        return ExitOk;
    }

    private static MatchModeCatalog? LoadCatalogOrNull(ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("STO.Server.Console");
        var path = Path.Combine(AppContext.BaseDirectory, MatchModeCsvRelativePath);
        try
        {
            return MatchModeCsv.LoadFile(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or IOException)
        {
            logger.LogError(ex, "Failed to load match-mode catalogue from {Path}", path);
            System.Console.Error.WriteLine($"sto-server: failed to load match-mode catalogue from {path}: {ex.Message}");
            return null;
        }
    }

    private static System.Collections.Generic.IEnumerable<string> IdsOf(MatchModeCatalog catalog)
    {
        foreach (var mode in catalog.Modes)
        {
            yield return mode.Id;
        }
    }
}
