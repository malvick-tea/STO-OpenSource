using System;
using System.Threading;
using System.Threading.Tasks;
using Garupan.Client.Core.Application;
using Garupan.Client.Core.Bootstrap;
using Garupan.Client.Core.Composition;
using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Navigation;
using Garupan.Client.Windows.Bootstrap;
using Garupan.Client.Windows.Direct3D12.Composition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Opus.Engine.Audio;
using Opus.Engine.Input.Sdl3;
using Opus.Engine.Pal.Application;
using Opus.Engine.Pal.Filesystem;
using Opus.Engine.Pal.Process;
using Opus.Engine.Pal.Time;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Pal.Windows.Threading;
using Opus.Engine.Ui.Direct3D12;
using Serilog;
using Serilog.Extensions.Logging;

namespace Garupan.Client.Windows.Direct3D12;

/// <summary>
/// Process entry point for the D3D12 client. Mirrors <c>Garupan.Client.Windows.WindowsEntry</c>
/// but routes window + draw surface + input through <see cref="D3D12HostBundle"/>. The
/// boot pipeline, DI container shape, error-return arithmetic, crash handler attachment,
/// and exit-channel wiring are identical — only the rendering host changes.
/// </summary>
public static class D3D12WindowsEntry
{
    private static readonly TimeSpan BootShutdownTimeout = TimeSpan.FromSeconds(5);

    public static int Main(string[] args)
    {
        _ = args;

        // Capture the main thread id BEFORE anything else touches threading.
        var dispatcher = new MainThreadDispatcher();

        SerilogSetup.Configure();
        using var serilogProvider = new SerilogLoggerProvider(Log.Logger, dispose: false);
        var bootLogger = serilogProvider.CreateLogger("STO.Bootstrap");

        try
        {
            using var container = BuildContainer(dispatcher, serilogProvider);
            ContainerAccessor.Set(container);

            using var shutdown = new ClientShutdownSignal(container.Resolve<ExitService>());
            var crashHandler = container.Resolve<ICrashHandler>();
            container.Resolve<CrashReportNotifier>().Attach(crashHandler);
            crashHandler.Install();

            RunSession(container, dispatcher, bootLogger, shutdown.Token);
            return 0;
        }
        catch (BootFailureException bex)
        {
            bootLogger.LogCritical(bex, "Boot failed at stage {Stage}", bex.Stage.Name);
            return 2;
        }
        catch (OperationCanceledException)
        {
            bootLogger.LogInformation("Cancelled by user.");
            return 0;
        }
        catch (InvalidOperationException iox) when (iox.Message.StartsWith("D3D12 host bring-up failed", StringComparison.Ordinal))
        {
            bootLogger.LogCritical(iox, "D3D12 host unavailable on this machine.");
            return 3;
        }
        catch (Exception ex)
        {
            bootLogger.LogCritical(ex, "Fatal: {Message}", ex.Message);
            return 1;
        }
        finally
        {
            ContainerAccessor.Clear();
            Log.CloseAndFlush();
        }
    }

    private static ClientContainer BuildContainer(
        MainThreadDispatcher dispatcher,
        SerilogLoggerProvider serilogProvider)
    {
        return ClientContainer.Build(services =>
        {
            services.AddSingleton<ILoggerProvider>(serilogProvider);
            services.AddLogging(b =>
            {
                b.ClearProviders();
                b.AddProvider(serilogProvider);
                b.SetMinimumLevel(LogLevel.Debug);
            });
            D3D12WindowsServicesModule.Register(services, dispatcher);
        });
    }

    private static void RunSession(
        ClientContainer container,
        MainThreadDispatcher dispatcher,
        Microsoft.Extensions.Logging.ILogger bootLogger,
        CancellationToken hostCt)
    {
        // The D3D12 host opens its window inside D3D12HostBundle (the swap chain needs a
        // live HWND at composition time) — before the boot pipeline's ConfigurationStage
        // runs. Load the persisted settings here so the bundle adopts the player's window
        // size; ConfigurationStage re-reads the same file later for the runtime snapshot.
        container.Resolve<SettingsService>().LoadAsync(hostCt).GetAwaiter().GetResult();

        // Resolving the host bundle brings up window + device + swap chain + atlas + draw
        // surface + frame loop + input source + resize bridge in one shot. Any failure
        // throws here, before the boot pipeline starts — Main catches it as exit code 3.
        var bundle = container.Resolve<D3D12HostBundle>();

        var window = bundle.Session.Window;
        var lifecycle = container.Resolve<ILifecycleService>();
        var vfs = container.Resolve<IVfs>();
        var input = bundle.Input;
        var audio = container.Resolve<IAudioDevice>();
        var clock = container.Resolve<IHighResClock>();
        var stack = container.Resolve<ScreenStack>();
        var drawSurface = bundle.DrawSurface;
        var frameLoop = bundle.FrameLoop;
        var sequence = container.Resolve<BootSequence>();

        var ctx = new BootContext(
            services: container.Services,
            window: window,
            vfs: vfs,
            lifecycle: lifecycle,
            mainThread: dispatcher,
            logger: bootLogger);

        using var bootCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);
        var bootTask = Task.Run(
            async () => await sequence.RunAsync(ctx, bootCts.Token).ConfigureAwait(false),
            bootCts.Token);

        D3D12WindowsFrameLoop.Run(window, dispatcher, stack, drawSurface, frameLoop, input, audio, clock, bootTask, hostCt);
        if (!bootTask.IsCompleted)
        {
            bootCts.Cancel();
            var deadline = DateTime.UtcNow + BootShutdownTimeout;
            while (!bootTask.IsCompleted && DateTime.UtcNow < deadline)
            {
                dispatcher.DrainPending();
                Thread.Sleep(1);
            }
        }

        if (!bootTask.IsCompleted)
        {
            throw new TimeoutException(
                $"Boot pipeline did not stop within {BootShutdownTimeout.TotalSeconds:0} seconds.");
        }

        bootTask.GetAwaiter().GetResult();
    }
}
