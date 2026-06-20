using System;
using System.Threading;
using Garupan.Client.Core.Application;

namespace Garupan.Client.Windows.Direct3D12;

internal sealed class ClientShutdownSignal : IDisposable
{
    private readonly ExitService _exitService;
    private readonly CancellationTokenSource _source = new();
    private bool _disposed;

    public ClientShutdownSignal(ExitService exitService)
    {
        ArgumentNullException.ThrowIfNull(exitService);
        _exitService = exitService;
        Console.CancelKeyPress += OnCancelKeyPress;
        _exitService.ExitRequested += OnExitRequested;
    }

    public CancellationToken Token => _source.Token;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Console.CancelKeyPress -= OnCancelKeyPress;
        _exitService.ExitRequested -= OnExitRequested;
        _source.Dispose();
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        _source.Cancel();
    }

    private void OnExitRequested() => _source.Cancel();
}
