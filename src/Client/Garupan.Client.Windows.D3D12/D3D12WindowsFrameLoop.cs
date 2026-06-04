using System;
using System.Threading;
using System.Threading.Tasks;
using Garupan.Client.Ui.Navigation;
using Opus.Engine.Audio;
using Opus.Engine.Input.Sdl3;
using Opus.Engine.Pal.Application;
using Opus.Engine.Pal.Time;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Pal.Windows.Threading;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12;
using Opus.Foundation;

namespace Garupan.Client.Windows.Direct3D12;

/// <summary>
/// D3D12 frame loop:
/// SDL event pump → screen update → frame loop barrier + draw surface bracket → present.
///
/// <para>Per-frame order (SDL has no implicit pump, so the loop drives each step
/// explicitly):</para>
/// <list type="number">
/// <item><description>Drain queued main-thread callbacks (screens pushed by boot stages on worker threads).</description></item>
/// <item><description><see cref="SdlWindowService.PollEvents"/> — SDL queue → input state.</description></item>
/// <item><description><see cref="ScreenStack.Update"/> — screens consume input + advance state.</description></item>
/// <item><description><see cref="D3D12UiFrameLoop.BeginFrame"/> + <see cref="D3D12DrawSurface.BeginFrame"/> bracket.</description></item>
/// <item><description><see cref="ScreenStack.Render"/> against the draw surface.</description></item>
/// <item><description><see cref="D3D12DrawSurface.EndFrame"/> + <see cref="D3D12UiFrameLoop.EndFrame"/> — execute + present.</description></item>
/// <item><description><see cref="SdlPolledInputSource.EndFrame"/> — clear rising-edge sets for the next frame.</description></item>
/// </list>
/// </summary>
internal static class D3D12WindowsFrameLoop
{
    /// <summary>Fallback delta when the clock returns a zero or negative tick (first
    /// frame, clock reset). Matches the simulation's 60 Hz target so the first
    /// <see cref="GameTime"/> looks like every other.</summary>
    private const double FallbackFrameSeconds = 1.0 / 60.0;

    private static readonly Color FrameClearColor = new(15, 18, 24, 255);

    public static void Run(
        IWindowService window,
        MainThreadDispatcher dispatcher,
        ScreenStack stack,
        D3D12DrawSurface drawSurface,
        D3D12UiFrameLoop frameLoop,
        SdlPolledInputSource input,
        IAudioDevice audio,
        IHighResClock clock,
        Task bootTask,
        CancellationToken ct)
    {
        var lastSeconds = clock.GetElapsedSeconds();
        var tick = Tick.Zero;

        while (window.IsOpen && !ct.IsCancellationRequested)
        {
            var now = clock.GetElapsedSeconds();
            var delta = Math.Max(0.0, now - lastSeconds);
            lastSeconds = now;
            var time = new GameTime(tick, delta <= 0 ? FallbackFrameSeconds : delta);
            tick += 1;

            dispatcher.DrainPending();
            window.PollEvents();

            stack.Update(time, input);
            audio.Update();

            var frame = frameLoop.BeginFrame();
            drawSurface.BeginFrame(
                frame.CommandList,
                frame.RenderTargetView,
                frame.BackBufferSlot,
                frame.ViewportWidth,
                frame.ViewportHeight);
            drawSurface.Clear(FrameClearColor);
            stack.Render(drawSurface);
            drawSurface.EndFrame();
            frameLoop.EndFrame();

            input.EndFrame();

            if (bootTask.IsFaulted)
            {
                break;
            }
        }
    }
}
