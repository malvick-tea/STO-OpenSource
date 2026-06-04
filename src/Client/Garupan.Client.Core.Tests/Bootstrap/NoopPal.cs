using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Opus.Engine.Pal.Application;
using Opus.Engine.Pal.Filesystem;
using Opus.Engine.Pal.Threading;

namespace Garupan.Client.Core.Tests.Bootstrap;

internal sealed class NoopWindowService : IWindowService
{
    public bool IsOpen => false;

    public (int Width, int Height) Size => (0, 0);

    public string Title { get; set; } = string.Empty;

    public event Action? Opened;

    public event Action? CloseRequested;

    public event Action<int, int>? Resized;

    public void Open(WindowOptions options)
    {
        _ = Opened;
        _ = CloseRequested;
        _ = Resized;
    }

    public void PollEvents()
    {
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }
}

internal sealed class NoopVfs : IVfs
{
    public bool Exists(string virtualPath) => false;

    public Stream OpenRead(string virtualPath) => Stream.Null;

    public Stream OpenWrite(string virtualPath) => Stream.Null;

    public Task WriteAllBytesAtomicAsync(string virtualPath, byte[] payload, CancellationToken ct) =>
        Task.CompletedTask;

    public string Realize(string virtualPath) => virtualPath;
}

internal sealed class NoopLifecycle : ILifecycleService
{
    public LifecycleState State => LifecycleState.Foreground;

    public event Action<LifecycleState, LifecycleState>? StateChanged;

    public event Action? ShuttingDown;

    public NoopLifecycle()
    {
        _ = StateChanged;
        _ = ShuttingDown;
    }
}

internal sealed class NoopDispatcher : IMainThreadDispatcher
{
    public bool IsOnMainThread => true;

    public void Post(Action callback) => callback();

    public Task InvokeAsync(Action callback)
    {
        callback();
        return Task.CompletedTask;
    }

    public Task<T> InvokeAsync<T>(Func<T> callback) => Task.FromResult(callback());
}
