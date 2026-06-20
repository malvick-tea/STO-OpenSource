using System;
using System.Diagnostics;
using System.Threading;
using Garupan.Server.Match;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Garupan.Server.Console;

/// <summary>
/// Wall-clock driver for <see cref="MatchHost.Pump"/>. Runs on the calling thread, sleeps
/// between frames to hit a target <c>FramePumpHz</c>, and stops cleanly when the supplied
/// <see cref="CancellationToken"/> is signalled.
/// </summary>
/// <remarks>
/// <para>
/// The tick loop is intentionally minimal: a <see cref="Stopwatch"/> measures the delta
/// between iterations and forwards it to <see cref="MatchHost.Pump"/>, which itself
/// drives a <see cref="Opus.Foundation.FixedStepLoop"/> internally. Whenever the
/// wall-clock pace exceeds the target frame budget the loop calls
/// <see cref="Thread.Sleep(int)"/> for the residue — guaranteed coarse on Windows
/// (default scheduler granularity ~15 ms) but plenty fine for a 120 Hz target.
/// </para>
/// <para>
/// The fixed-step accumulator inside <c>MatchHost</c> means a coarse pump still produces
/// deterministic per-tick behaviour; we just have a bit more pump-to-tick jitter than a
/// real-time graphics loop would tolerate. Acceptable trade for a server.
/// </para>
/// </remarks>
public sealed class MatchHostTickLoop
{
    private const int TelemetryEveryNFrames = 600;

    private readonly MatchHost _host;
    private readonly TimeSpan _targetFrameInterval;
    private readonly ILogger<MatchHostTickLoop> _logger;
    private readonly Action? _beforePump;
    private int _framesPumped;
    private int _telemetryFrameCounter;

    public MatchHostTickLoop(
        MatchHost host,
        int framePumpHz,
        ILogger<MatchHostTickLoop>? logger = null,
        Action? beforePump = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (framePumpHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(framePumpHz), framePumpHz, "framePumpHz must be positive");
        }

        _host = host;
        _targetFrameInterval = TimeSpan.FromSeconds(1.0 / framePumpHz);
        _logger = logger ?? NullLogger<MatchHostTickLoop>.Instance;
        _beforePump = beforePump;
    }

    /// <summary>How many <see cref="MatchHost.Pump"/> calls have happened since the loop
    /// started. Tests assert against this.</summary>
    public int FramesPumped => Volatile.Read(ref _framesPumped);

    /// <summary>Pumps the loop on the calling thread until <paramref name="cancellation"/>
    /// is cancelled. Returns normally on cancellation; rethrows any other exception
    /// raised inside <see cref="MatchHost.Pump"/>.</summary>
    public void Run(CancellationToken cancellation)
    {
        var stopwatch = Stopwatch.StartNew();
        var lastTimestamp = stopwatch.Elapsed;

        _logger.LogInformation(
            "Server tick loop started: target pump = {PumpHz} Hz ({Interval:F2} ms/frame).",
            (int)Math.Round(1.0 / _targetFrameInterval.TotalSeconds),
            _targetFrameInterval.TotalMilliseconds);

        while (!cancellation.IsCancellationRequested)
        {
            var now = stopwatch.Elapsed;
            var deltaSeconds = (now - lastTimestamp).TotalSeconds;
            lastTimestamp = now;

            _beforePump?.Invoke();
            _host.Pump(deltaSeconds);
            Interlocked.Increment(ref _framesPumped);
            EmitTelemetryIfDue();

            var elapsedInFrame = stopwatch.Elapsed - now;
            var residue = _targetFrameInterval - elapsedInFrame;
            if (residue > TimeSpan.Zero)
            {
                SleepCancellable(residue, cancellation);
            }
        }

        _logger.LogInformation(
            "Server tick loop stopped: {Frames} frames pumped, {Snapshots} snapshots fanned out.",
            _framesPumped,
            _host.SnapshotsBroadcast);
    }

    private static void SleepCancellable(TimeSpan duration, CancellationToken cancellation)
    {
        if (duration <= TimeSpan.Zero || cancellation.IsCancellationRequested)
        {
            return;
        }

        // WaitHandle.WaitOne short-circuits the moment cancellation is signalled — better
        // than Thread.Sleep for snappier shutdown when a Ctrl+C lands mid-sleep.
        cancellation.WaitHandle.WaitOne(duration);
    }

    private void EmitTelemetryIfDue()
    {
        _telemetryFrameCounter++;
        if (_telemetryFrameCounter < TelemetryEveryNFrames)
        {
            return;
        }

        _telemetryFrameCounter = 0;
        _logger.LogInformation(
            "Server tick: tick={Tick}, players={Players}, snapshots={Snapshots}.",
            _host.CurrentTick.Value,
            _host.PlayerCount,
            _host.SnapshotsBroadcast);
    }
}
