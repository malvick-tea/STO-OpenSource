using System;
using System.Numerics;
using Garupan.Content;
using Garupan.Server.Match;
using Garupan.Sim.Components;
using Microsoft.Extensions.Logging;
using Opus.Net.Udp.Transport;

namespace Garupan.Server.Console;

/// <summary>
/// Composition aggregate for one running server: owns its <see cref="UdpServerTransport"/>,
/// its <see cref="MatchHost"/>, and the <see cref="MatchHostTickLoop"/> wall-clock driver.
/// Built once in <c>Program.Main</c> (or in a test fixture) and disposed on shutdown.
/// </summary>
/// <remarks>
/// <para>
/// The bundle deliberately stays small — it just wires existing primitives. The closed
/// alpha's first runtime artifact is "one server, one room"; multi-room hosting (a
/// `MatchRoomRegistry` owning N bundles, one per room) lands in a later phase.
/// </para>
/// <para>
/// <see cref="Dispose"/> tears the layer in reverse construction order: stop the host
/// (releases its world + session), then close the transport (releases the socket + joins
/// the receive worker). The tick loop holds no state besides counters and needs no
/// disposal.
/// </para>
/// </remarks>
public sealed class ServerHostBundle : IDisposable
{
    private readonly UdpServerTransport _transport;
    private readonly MatchHost _host;
    private readonly MatchHostTickLoop _tickLoop;
    private readonly ILogger<ServerHostBundle> _logger;
    private bool _disposed;

    private ServerHostBundle(
        UdpServerTransport transport,
        MatchHost host,
        MatchHostTickLoop tickLoop,
        ILogger<ServerHostBundle> logger)
    {
        _transport = transport;
        _host = host;
        _tickLoop = tickLoop;
        _logger = logger;
    }

    /// <summary>The bound UDP endpoint the server is listening on. Useful for tests that
    /// passed <c>port=0</c> and need the OS-assigned port to connect a client.</summary>
    public System.Net.IPEndPoint BoundEndpoint => _transport.BoundEndpoint;

    /// <summary>The match host. Exposed so tests can assert player count / snapshot
    /// counters; runtime code never touches it directly.</summary>
    public MatchHost Host => _host;

    /// <summary>The wall-clock tick loop. Exposed so runtime code can call
    /// <see cref="MatchHostTickLoop.Run"/> on the calling thread + tests can read
    /// <see cref="MatchHostTickLoop.FramesPumped"/> for cadence assertions.</summary>
    public MatchHostTickLoop TickLoop => _tickLoop;

    /// <summary>Builds + binds a server with the supplied configuration. The match-host
    /// options are derived from <paramref name="mode"/> via
    /// <see cref="MatchHostOptionsFactory.ForMode"/>; the caller resolves the mode id
    /// from the catalogue before constructing the bundle. Throws
    /// <see cref="System.Net.Sockets.SocketException"/> when the UDP bind fails (port in
    /// use is the usual cause); the caller surfaces that as an exit-code failure.</summary>
    public static ServerHostBundle Create(
        ServerConsoleOptions options,
        MatchMode mode,
        ILoggerFactory loggerFactory,
        UdpTransportOptions? transportOptions = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(mode);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var transport = UdpServerTransport.Bind(
            name: "sto-server",
            listenEndpoint: options.ListenEndpoint,
            options: transportOptions,
            logger: loggerFactory.CreateLogger<UdpServerTransport>());

        var map = DefaultMatchMapLoader.TryLoad(AppContext.BaseDirectory);
        var matchOptions = MatchHostOptionsFactory.ForMode(
            mode,
            playerSpec: TankRoster.VehicleMediumB,
            spawnAnchor: Vector2.Zero,
            tickRateHz: options.TickRateHz,
            snapshotIntervalTicks: options.SnapshotIntervalTicks) with
        {
            TerrainHeightSampler = map?.TerrainHeightSampler,
            MapProps = map?.Props ?? Array.Empty<MapProp>(),
            MapObstacles = map?.Obstacles ?? Array.Empty<MapObstacle>(),
        };

        var host = new MatchHost(transport, matchOptions, loggerFactory.CreateLogger<MatchHost>());
        var tickLoop = new MatchHostTickLoop(host, options.FramePumpHz, loggerFactory.CreateLogger<MatchHostTickLoop>());

        var logger = loggerFactory.CreateLogger<ServerHostBundle>();
        logger.LogInformation(
            "Server bound on {Endpoint}: mode={Mode}, kind={Kind}, respawns={Respawns}, tick={TickHz}Hz, snapshot-every={Snapshot}t, pump={PumpHz}Hz, map={Map}.",
            transport.BoundEndpoint,
            mode.Id,
            mode.Kind,
            mode.RespawnLimit,
            options.TickRateHz,
            options.SnapshotIntervalTicks,
            options.FramePumpHz,
            map?.Id ?? "flat");

        return new ServerHostBundle(transport, host, tickLoop, logger);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _logger.LogInformation("Shutting down server host…");
        _host.Dispose();
        _transport.Dispose();
    }
}
