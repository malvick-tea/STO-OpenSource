using System;
using Microsoft.Extensions.Logging;
using Opus.Engine.Pal.Application;
using Opus.Engine.Pal.Filesystem;
using Opus.Engine.Pal.Threading;

namespace Garupan.Client.Core.Bootstrap;

/// <summary>
/// Carries the host services that boot stages may need: window, vfs, lifecycle,
/// dispatcher, the root service provider, and a logger pre-configured for boot.
///
/// Stages should NOT capture this — read what they need at <c>ExecuteAsync</c> time.
/// The legacy Godot-flavoured <c>BootContext</c> exposed <c>SceneTree</c> + <c>Node</c>;
/// this rewrite is host-agnostic.
/// </summary>
public sealed class BootContext
{
    public BootContext(
        IServiceProvider services,
        IWindowService window,
        IVfs vfs,
        ILifecycleService lifecycle,
        IMainThreadDispatcher mainThread,
        ILogger logger)
    {
        Services = services;
        Window = window;
        Vfs = vfs;
        Lifecycle = lifecycle;
        MainThread = mainThread;
        Logger = logger;
    }

    public IServiceProvider Services { get; }

    public IWindowService Window { get; }

    public IVfs Vfs { get; }

    public ILifecycleService Lifecycle { get; }

    public IMainThreadDispatcher MainThread { get; }

    public ILogger Logger { get; }
}
