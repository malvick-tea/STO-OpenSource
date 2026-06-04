using System;
using Microsoft.Extensions.Logging;
using Opus.Foundation;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Garupan.Client.Core.Logging;

/// <summary>
/// Bridges <c>Microsoft.Extensions.Logging.ILogger</c> down to the layer-light
/// <see cref="ILog"/> contract that Foundation / Sim / Content code uses. Lets us keep
/// the structured-logging stack at the host level without polluting lower layers.
/// </summary>
public sealed class MicrosoftLogAdapter : ILog
{
    private readonly Microsoft.Extensions.Logging.ILogger _inner;

    public MicrosoftLogAdapter(Microsoft.Extensions.Logging.ILogger inner)
    {
        _inner = Ensure.NotNull(inner);
    }

    public bool IsEnabled(Opus.Foundation.LogLevel level) => _inner.IsEnabled(Map(level));

    public void Log(Opus.Foundation.LogLevel level, string message, Exception? exception = null)
    {
#pragma warning disable CA2254 // template stays literal — this adapter forwards already-formatted strings
        if (exception is null)
        {
            _inner.Log(Map(level), message);
        }
        else
        {
            _inner.Log(Map(level), exception, message);
        }
#pragma warning restore CA2254
    }

    private static MsLogLevel Map(Opus.Foundation.LogLevel level) => level switch
    {
        Opus.Foundation.LogLevel.Trace => MsLogLevel.Trace,
        Opus.Foundation.LogLevel.Debug => MsLogLevel.Debug,
        Opus.Foundation.LogLevel.Information => MsLogLevel.Information,
        Opus.Foundation.LogLevel.Warning => MsLogLevel.Warning,
        Opus.Foundation.LogLevel.Error => MsLogLevel.Error,
        Opus.Foundation.LogLevel.Critical => MsLogLevel.Critical,
        _ => MsLogLevel.Information,
    };
}
