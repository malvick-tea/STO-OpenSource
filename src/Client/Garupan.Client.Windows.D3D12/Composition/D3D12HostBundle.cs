using System;
using Garupan.Client.Core.Application;
using Garupan.Client.Windows.Bootstrap;
using Opus.Engine.Input.Sdl3;
using Opus.Engine.Pal.Application;
using Opus.Engine.Pal.Filesystem;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Ui.Direct3D12;
using Opus.Engine.Ui.Direct3D12.Text;

namespace Garupan.Client.Windows.Direct3D12.Composition;

/// <summary>
/// Disposable composition of the six objects the D3D12 client needs to render one
/// frame: <see cref="D3D12WindowSession"/> (window + device + swap chain + compiler),
/// <see cref="D3D12FontAtlas"/> (CJK + Latin atlas baked from bundled locale text),
/// <see cref="D3D12DrawSurface"/> (the runtime <c>IDrawSurface</c> impl),
/// <see cref="D3D12UiFrameLoop"/> (per-frame barriers + Present),
/// <see cref="SdlPolledInputSource"/> (SDL events → poll-based input contract), and
/// <see cref="D3D12WindowResizeBridge"/> (keeps the swap chain sized to the window).
///
/// <para>Lifecycle: <see cref="Open"/> brings everything up in dependency order;
/// <see cref="Dispose"/> tears down in reverse so the swap chain releases its back
/// buffers while the device is still alive. The bundle owns every object — callers
/// borrow through the public properties and must NOT dispose them individually.</para>
/// </summary>
public sealed class D3D12HostBundle : IDisposable
{
    private const float AtlasPixelHeight = D3D12FontAtlas.DefaultPixelHeight;
    private const int AtlasSize = D3D12FontAtlas.DefaultAtlasSize;
    private const string WindowTitle = "STO";

    private readonly D3D12WindowSession _session;
    private readonly D3D12FontAtlas _atlas;
    private readonly D3D12DrawSurface _drawSurface;
    private readonly D3D12UiFrameLoop _frameLoop;
    private readonly SdlPolledInputSource _input;
    private readonly D3D12WindowResizeBridge _resizeBridge;
    private bool _disposed;

    private D3D12HostBundle(
        D3D12WindowSession session,
        D3D12FontAtlas atlas,
        D3D12DrawSurface drawSurface,
        D3D12UiFrameLoop frameLoop,
        SdlPolledInputSource input,
        D3D12WindowResizeBridge resizeBridge)
    {
        _session = session;
        _atlas = atlas;
        _drawSurface = drawSurface;
        _frameLoop = frameLoop;
        _input = input;
        _resizeBridge = resizeBridge;
    }

    public D3D12WindowSession Session => _session;

    public D3D12DrawSurface DrawSurface => _drawSurface;

    public D3D12UiFrameLoop FrameLoop => _frameLoop;

    public SdlPolledInputSource Input => _input;

    /// <summary>Brings up the bundle from a resolved <see cref="IVfs"/> and the player's
    /// persisted <see cref="AppSettings"/>: the window adopts
    /// <see cref="AppSettings.WindowWidth"/> / <see cref="AppSettings.WindowHeight"/> and
    /// opens resizable. Throws <see cref="InvalidOperationException"/> when SDL or D3D12 is
    /// unavailable on the host — the entry point catches it and surfaces a boot failure
    /// return code.</summary>
    public static D3D12HostBundle Open(IVfs vfs, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(vfs);
        ArgumentNullException.ThrowIfNull(settings);

        // VSync is left off here: the D3D12 UI frame loop presents at sync-interval 0, so
        // WindowOptions.VSync is inert on this path until D3D12UiFrameLoop reads it.
        var session = D3D12WindowSession.TryOpen(new D3D12WindowSessionOptions(
            new WindowOptions(
                WindowTitle,
                settings.WindowWidth,
                settings.WindowHeight,
                Resizable: true,
                VSync: false,
                WindowMode.Windowed),
            EnableDebugLayer: false))
            ?? throw new InvalidOperationException(
                "D3D12 host bring-up failed: no D3D12-capable adapter, or SDL video subsystem unavailable.");

        D3D12FontAtlas? atlas = null;
        D3D12DrawSurface? drawSurface = null;
        D3D12UiFrameLoop? frameLoop = null;
        SdlPolledInputSource? input = null;
        D3D12WindowResizeBridge? resizeBridge = null;
        try
        {
            atlas = D3D12FontAtlas.BuildAndUpload(
                session.Device,
                LocalizationFontGlyphs.ReadCorpus(vfs),
                AtlasPixelHeight,
                AtlasSize);
            drawSurface = D3D12DrawSurface.Create(session.Device, atlas, session.Compiler, session.SwapChain.Format);
            frameLoop = new D3D12UiFrameLoop(session);
            input = new SdlPolledInputSource(session.Window);
            resizeBridge = new D3D12WindowResizeBridge(session.Window, session.SwapChain.Resize);

            var bundle = new D3D12HostBundle(session, atlas, drawSurface, frameLoop, input, resizeBridge);
            // Ownership transferred: null the locals so the catch path (which
            // only runs if a later statement throws) does not double-dispose
            // resources the bundle now owns.
            atlas = null;
            drawSurface = null;
            frameLoop = null;
            input = null;
            resizeBridge = null;
            return bundle;
        }
        catch
        {
            // Tear down everything that was allocated before the throw, in
            // reverse construction order. The session is disposed
            // unconditionally here because the bundle never took ownership —
            // the previous implementation only disposed it when at least one
            // local was non-null, which leaked the entire D3D12 device + swap
            // chain + compiler when the very first in-try call (BuildAndUpload)
            // threw.
            resizeBridge?.Dispose();
            input?.Dispose();
            frameLoop?.Dispose();
            drawSurface?.Dispose();
            atlas?.Dispose();
            session.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _resizeBridge.Dispose();
        _input.Dispose();
        _frameLoop.Dispose();
        _drawSurface.Dispose();
        _atlas.Dispose();
        _session.Dispose();
        _disposed = true;
    }
}
