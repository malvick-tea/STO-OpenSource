using System;
using System.Globalization;
using System.IO;
using Serilog;
using Serilog.Events;

namespace Garupan.Client.Windows.Bootstrap;

/// <summary>
/// Configures the process-wide Serilog logger before anything else touches the logging
/// pipeline. Two sinks:
/// <list type="bullet">
/// <item><description>Console — short timestamp, useful for live runs.</description></item>
/// <item><description>Rolling file at <c>%LOCALAPPDATA%\Garupan\logs\garupan-yyyy-MM-dd.log</c>, 7-day retention. Survives a crash so post-mortem analysis has the same line a console sink would.</description></item>
/// </list>
/// Pulled out of <see cref="WindowsEntry"/> so the entry point stays focused on
/// composition and error-return arithmetic.
/// </summary>
public static class SerilogSetup
{
    public static void Configure()
    {
        var logsDir = ResolveLogsDirectory();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(LogEventLevel.Debug)
            .Enrich.WithProperty("Pid", Environment.ProcessId)
            .WriteTo.Console(
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.File(
                path: Path.Combine(logsDir, "sto-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();
    }

    private static string ResolveLogsDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logsDir = Path.Combine(localAppData, "STO", "logs");
        try
        {
            Directory.CreateDirectory(logsDir);
        }
        catch
        {
            // user:// is always writable on supported platforms; if it somehow fails the
            // console sink still works and the file sink will surface the error itself.
        }

        return logsDir;
    }
}
