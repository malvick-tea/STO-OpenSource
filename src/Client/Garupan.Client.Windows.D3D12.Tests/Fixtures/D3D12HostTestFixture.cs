using System;
using Opus.Engine.Pal.Windows.Direct3D12;
using Xunit;

namespace Garupan.Client.Windows.Direct3D12.Tests.Fixtures;

/// <summary>
/// Process-scoped singleton wrapper around <see cref="D3D12WindowSession"/>. Every D3D12
/// GPU-touching test across test assemblies acquires the session through
/// <see cref="TryAcquire"/> so the SDL video subsystem, the D3D12 device, and the DXC
/// compiler are brought up exactly once per <c>testhost.exe</c>.
///
/// <para><b>Why singletons:</b> the NVIDIA user-mode driver caps cumulative
/// <c>D3D12CreateDevice</c> calls at ~30 per process before kernel-mode resource
/// exhaustion freezes <c>testhost.exe</c> (the same rationale that drives
/// <c>Engine.Rhi.D3D12.Tests/Harness/D3D12TestHarness</c>). SDL compounds the issue —
/// its DLL handle stays pinned in the loader across <c>SDL_QuitSubSystem(VIDEO)</c>
/// calls, so re-init churn eventually exhausts loader slots.</para>
///
/// <para>The singleton is torn down on <see cref="AppDomain.ProcessExit"/>; consumers
/// must NOT dispose the session themselves — that would invalidate it for every
/// subsequent test in the same process.</para>
/// </summary>
public static class D3D12HostTestFixture
{
    private const int SharedWidth = 320;
    private const int SharedHeight = 240;
    private const string SharedTitle = "sto.d3d12.testfixture";

    private static readonly object Gate = new();
    private static D3D12WindowSession? _session;
    private static bool _initialiseFailed;
    private static bool _processExitHooked;

    /// <summary>Returns the singleton session, lazily brought up on first call. Returns
    /// <c>null</c> when SDL video or a D3D12 adapter is unavailable; callers must
    /// <c>Skip.If(session is null, ...)</c> before dereferencing.</summary>
    public static D3D12WindowSession? TryAcquire()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        lock (Gate)
        {
            if (_initialiseFailed)
            {
                return null;
            }

            if (_session is not null)
            {
                return _session;
            }

            var session = D3D12WindowSession.TryOpen(
                D3D12WindowSessionOptions.Windowed(SharedTitle, SharedWidth, SharedHeight));
            if (session is null)
            {
                _initialiseFailed = true;
                return null;
            }

            _session = session;
            HookProcessExitOnce();
            return _session;
        }
    }

    /// <summary>Convenience: <c>Skip.If</c> when the shared session couldn't initialise.
    /// Same diagnostic surface as the Rhi.D3D12 harness so test failures read uniformly
    /// across the suite.</summary>
    public static void SkipIfUnavailable(D3D12WindowSession? session) =>
        Skip.If(session is null, "No D3D12-capable adapter or SDL video subsystem on this host.");

    private static void HookProcessExitOnce()
    {
        if (_processExitHooked)
        {
            return;
        }

        AppDomain.CurrentDomain.ProcessExit += static (_, _) => DisposeSingleton();
        _processExitHooked = true;
    }

    private static void DisposeSingleton()
    {
        lock (Gate)
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
