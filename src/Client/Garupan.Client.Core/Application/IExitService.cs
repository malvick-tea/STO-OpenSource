using System;

namespace Garupan.Client.Core.Application;

/// <summary>
/// Channel for screens / view-models to ask the host to terminate. The host (Client.Windows /
/// Client.Android / Client.iOS) subscribes to <see cref="ExitRequested"/> and triggers its
/// own shutdown path — usually cancelling the boot/frame-loop CTS, then disposing the container.
/// </summary>
public interface IExitService
{
    event Action? ExitRequested;

    void RequestExit();
}

public sealed class ExitService : IExitService
{
    public event Action? ExitRequested;

    public void RequestExit() => ExitRequested?.Invoke();
}
