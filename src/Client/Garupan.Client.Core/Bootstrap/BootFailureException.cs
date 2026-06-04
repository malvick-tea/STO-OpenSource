using System;

namespace Garupan.Client.Core.Bootstrap;

/// <summary>
/// Wraps the original exception thrown by a boot stage so callers can render a
/// stage-aware error message ("Failed at: Localization") without losing the inner trace.
/// </summary>
public sealed class BootFailureException : Exception
{
    public BootFailureException(IBootStage stage, Exception inner)
        : base($"Boot stage '{stage.Name}' failed.", inner)
    {
        Stage = stage;
    }

    public IBootStage Stage { get; }
}
