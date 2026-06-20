using System;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace Garupan.Server.Console;

/// <summary>
/// Entry point for the Garupan authoritative match server. Wires Serilog → ILoggerFactory,
/// installs Ctrl+C → <see cref="ShutdownSignal"/>, delegates the actual run loop to
/// <see cref="ServerConsoleEntry.Run"/>. Keeping the work behind <c>Run</c> means the
/// boot pipeline is unit-testable; this <c>Main</c> stays focused on host-level wiring
/// that needs the real process.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        var loggerFactory = ServerSerilogSetup.Configure(
            fileSinkEnabled: PreviewFileLoggingFlag(args),
            minimumLevel: PreviewLogLevel(args));
        try
        {
            using var shutdown = new ShutdownSignal();
            return ServerConsoleEntry.Run(args, loggerFactory, shutdown);
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("STO.Server.Console")
                .LogCritical(ex, "Unhandled server exception.");
            System.Console.Error.WriteLine(
                $"sto-server: unhandled exception: {ex.Message}");
            return 99;
        }
        finally
        {
            loggerFactory.Dispose();
            ServerSerilogSetup.Shutdown();
        }
    }

    private static LogEventLevel PreviewLogLevel(string[] args)
    {
        for (var index = 0; index + 1 < args.Length; index++)
        {
            if (!string.Equals(args[index], "--log-level", StringComparison.Ordinal))
            {
                continue;
            }

            return args[index + 1].ToLowerInvariant() switch
            {
                "trace" => LogEventLevel.Verbose,
                "debug" => LogEventLevel.Debug,
                "warning" => LogEventLevel.Warning,
                "error" => LogEventLevel.Error,
                "critical" => LogEventLevel.Fatal,
                _ => LogEventLevel.Information,
            };
        }

        return LogEventLevel.Information;
    }

    /// <summary>Cheap pre-parse pass: peek at whether <c>--no-file-log</c> is set so the
    /// Serilog configuration matches the resolved options. The authoritative parse runs
    /// inside <see cref="ServerConsoleEntry.Run"/>; this only governs the file sink.</summary>
    private static bool PreviewFileLoggingFlag(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg == "--no-file-log")
            {
                return false;
            }
        }

        return true;
    }
}
