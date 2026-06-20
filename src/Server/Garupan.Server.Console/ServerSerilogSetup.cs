using System;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace Garupan.Server.Console;

/// <summary>
/// Server-side Serilog configuration. Mirrors the client's
/// <c>Garupan.Client.Windows.Bootstrap.SerilogSetup</c> in shape (console + optional
/// rolling file sink) but lives separately because the server lifecycle is decoupled
/// from the client bootstrap and the log directory under <c>%LOCALAPPDATA%\Garupan\server-logs</c>
/// is intentionally distinct from the client's so a single dev box can run both at once
/// without clashing log files.
/// </summary>
public static class ServerSerilogSetup
{
    /// <summary>Configures the process-wide Serilog logger and returns an
    /// <see cref="ILoggerFactory"/> wired to it. The caller owns the returned factory and
    /// must dispose it on shutdown — disposing flushes any buffered console / file
    /// writes.</summary>
    public static ILoggerFactory Configure(
        bool fileSinkEnabled,
        LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        var config = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.WithProperty("Pid", Environment.ProcessId)
            .WriteTo.Console(
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture);

        if (fileSinkEnabled)
        {
            var logsDir = ResolveLogsDirectory();
            config = config.WriteTo.File(
                path: Path.Combine(logsDir, "sto-server-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture);
        }

        Log.Logger = config.CreateLogger();
        return new SerilogLoggerFactory(Log.Logger, dispose: false);
    }

    /// <summary>Forces any buffered log lines to flush + clears the static Serilog logger.
    /// Called on graceful shutdown after the factory has been disposed.</summary>
    public static void Shutdown() => Log.CloseAndFlush();

    private static string ResolveLogsDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logsDir = Path.Combine(localAppData, "STO", "server-logs");
        try
        {
            Directory.CreateDirectory(logsDir);
        }
        catch
        {
            // If LocalAppData is unwritable the console sink still works; surfacing this
            // as a hard error would prevent a server from booting just because of a
            // permissions hiccup that doesn't affect gameplay.
        }

        return logsDir;
    }
}
