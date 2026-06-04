using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Garupan.Client.Core.Bootstrap;
using Garupan.Client.Core.Composition;
using Garupan.Client.Ui.Navigation;
using Garupan.Client.Ui.Screens.MainMenu;
using Garupan.Client.Windows.Direct3D12.Composition;
using Garupan.Client.Windows.Direct3D12.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Opus.Engine.Pal.Application;
using Opus.Engine.Pal.Filesystem;
using Opus.Engine.Pal.Windows.Threading;
using Opus.Engine.Ui;
using Opus.Foundation;
using Xunit;

namespace Garupan.Client.Windows.Direct3D12.Tests.Boot;

/// <summary>Phase D3 boot smoke. Brings the D3D12 client up the way the runtime entry
/// point does — real DI container (<see cref="ClientContainer"/> + the runtime
/// <see cref="D3D12WindowsServicesModule"/>), real <see cref="D3D12HostBundle"/>, real
/// <see cref="BootSequence"/> — and proves the pipeline reaches the main menu and paints
/// frames on the D3D12 host without an exception.
/// <para>
/// The boot pipeline runs on a worker task while the test thread drains the dispatcher
/// and renders, mirroring <c>D3D12WindowsFrameLoop</c>: <c>SplashPushStage</c> /
/// <c>InitialScreenStage</c> marshal their <see cref="ScreenStack"/> mutations back to
/// the test thread through <see cref="MainThreadDispatcher"/>, so the screen stack is
/// only ever touched on this (the "main") thread.
/// </para></summary>
public sealed class D3D12ClientBootSmokeTests
{
    /// <summary>Frames driven after boot completes. Each advances the screen stack by a
    /// 1/60 s tick — 48 ticks (0.8 s) outlast the 0.5 s splash→menu fade so the stack
    /// settles on the main menu.</summary>
    private const int PostBootFrames = 48;

    /// <summary>Upper bound on the whole boot. Boot is normally well under a second; this
    /// only exists so a hung stage fails the test instead of hanging the run.</summary>
    private static readonly TimeSpan BootTimeout = TimeSpan.FromSeconds(30);

    private static readonly Color FrameClearColor = new(15, 18, 24, 255);

    [SkippableFact]
    public void Boots_through_the_splash_to_the_main_menu_on_the_d3d12_host()
    {
        var hostProbe = D3D12HostTestFixture.TryAcquire();
        D3D12HostTestFixture.SkipIfUnavailable(hostProbe);

        var dispatcher = new MainThreadDispatcher();
        using var container = ClientContainer.Build(
            services => D3D12WindowsServicesModule.Register(services, dispatcher));

        // Resolving the bundle brings up the D3D12 window + device + swap chain + draw
        // surface + frame loop in one shot — the same composition the runtime exe uses.
        var bundle = container.Resolve<D3D12HostBundle>();
        var stack = container.Resolve<ScreenStack>();

        RunBootPipeline(container, dispatcher, bundle, stack);
        DriveFrames(bundle, stack, PostBootFrames);

        stack.Current.Should().BeOfType<MainMenuScreen>(
            "the boot pipeline replaces the splash with the main menu once every stage completes");
        stack.IsTransitioning.Should().BeFalse("the splash→menu fade has fully settled");
        stack.Depth.Should().Be(1, "the main menu replaces the splash rather than stacking on it");
    }

    private static void RunBootPipeline(
        ClientContainer container,
        MainThreadDispatcher dispatcher,
        D3D12HostBundle bundle,
        ScreenStack stack)
    {
        var ctx = new BootContext(
            services: container.Services,
            window: bundle.Session.Window,
            vfs: container.Resolve<IVfs>(),
            lifecycle: container.Resolve<ILifecycleService>(),
            mainThread: dispatcher,
            logger: NullLogger.Instance);

        using var cts = new CancellationTokenSource(BootTimeout);
        var bootSequence = container.Resolve<BootSequence>();
        var bootTask = Task.Run(() => bootSequence.RunAsync(ctx, cts.Token), cts.Token);

        var watchdog = Stopwatch.StartNew();
        var tick = Tick.Zero;
        while (!bootTask.IsCompleted)
        {
            dispatcher.DrainPending();
            RenderFrame(bundle, stack, tick);
            tick += 1;
            if (watchdog.Elapsed > BootTimeout)
            {
                break;
            }
        }

        // Run any callback queued by the final stage, then surface a stage failure or a
        // timeout cancellation as the test result.
        dispatcher.DrainPending();
        bootTask.GetAwaiter().GetResult();
    }

    private static void DriveFrames(D3D12HostBundle bundle, ScreenStack stack, int frames)
    {
        var tick = Tick.Zero;
        for (var i = 0; i < frames; i++)
        {
            RenderFrame(bundle, stack, tick);
            tick += 1;
        }
    }

    private static void RenderFrame(D3D12HostBundle bundle, ScreenStack stack, Tick tick)
    {
        bundle.Session.Window.PollEvents();

        var frame = bundle.FrameLoop.BeginFrame();
        bundle.DrawSurface.BeginFrame(
            frame.CommandList,
            frame.RenderTargetView,
            frame.BackBufferSlot,
            frame.ViewportWidth,
            frame.ViewportHeight);
        bundle.DrawSurface.Clear(FrameClearColor);
        stack.Update(new GameTime(tick, 1.0 / 60.0), bundle.Input);
        stack.Render(bundle.DrawSurface);
        bundle.DrawSurface.EndFrame();
        bundle.FrameLoop.EndFrame();
        bundle.Input.EndFrame();
    }
}
