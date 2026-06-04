using System;
using System.Threading;

namespace Garupan.Server.Console;

/// <summary>
/// Wraps the platform-level Ctrl+C / SIGTERM signals into a single
/// <see cref="CancellationTokenSource"/> that the tick loop can observe. First signal
/// cancels gracefully; a second within the grace window arms a hard exit by leaving the
/// process-exit handler in the OS default state.
/// </summary>
public sealed class ShutdownSignal : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ConsoleCancelEventHandler _cancelHandler;
    private readonly EventHandler _processExitHandler;
    private int _signalCount;
    private int _disposed;

    /// <summary>Installs Ctrl+C + ProcessExit handlers that flip
    /// <see cref="Token"/> on first signal. Returns immediately — the loop owns the
    /// shutdown cadence.</summary>
    public ShutdownSignal()
    {
        _cancelHandler = OnCancelKeyPress;
        _processExitHandler = OnProcessExit;
        System.Console.CancelKeyPress += _cancelHandler;
        AppDomain.CurrentDomain.ProcessExit += _processExitHandler;
    }

    /// <summary>The token the tick loop observes. Becomes cancelled on the first Ctrl+C
    /// or process-exit signal.</summary>
    public CancellationToken Token => _cts.Token;

    /// <summary>True once a signal has fired. Useful for tests that drive
    /// <see cref="Signal"/> directly without hitting <see cref="System.Console"/>.</summary>
    public bool HasFired => Volatile.Read(ref _signalCount) > 0;

    /// <summary>Programmatic shutdown — same effect as Ctrl+C. Idempotent.</summary>
    public void Signal()
    {
        Interlocked.Increment(ref _signalCount);
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        System.Console.CancelKeyPress -= _cancelHandler;
        AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;
        _cts.Dispose();
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        Signal();
        e.Cancel = Volatile.Read(ref _signalCount) <= 1;
    }

    private void OnProcessExit(object? sender, EventArgs e) => Signal();
}
