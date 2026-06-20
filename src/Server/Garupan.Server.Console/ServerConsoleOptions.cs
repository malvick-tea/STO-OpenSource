using System.Net;
using Microsoft.Extensions.Logging;

namespace Garupan.Server.Console;

/// <summary>
/// Resolved configuration for one <c>Garupan.Server.Console</c> process — populated by
/// <see cref="ServerConsoleOptionsParser"/> from the command line, the environment, or
/// (in tests) constructed directly with the explicit values needed for a scenario.
/// </summary>
/// <param name="ListenEndpoint">UDP endpoint the server binds to. Use port 0 for an
/// ephemeral (every test does this); runtime deployments pick a fixed port the
/// client build is configured to dial.</param>
/// <param name="TickRateHz">Authoritative sim tick rate. Forwarded into the
/// <c>MatchHostOptions.TickRateHz</c> default. Matches the canonical Phase-0 60 Hz.</param>
/// <param name="SnapshotIntervalTicks">How often the host fans out a snapshot. <c>1</c> =
/// every tick (highest fidelity, highest bandwidth); <c>3</c> = 20 Hz on a 60 Hz tick —
/// closer to a runtime cadence but still smoke-friendly. Default conservatively snaps
/// every tick so the local test runs see the same wire density as the integration
/// tests.</param>
/// <param name="FramePumpHz">Wall-clock frequency the tick loop's `Pump` is called at —
/// it deliberately exceeds <see cref="TickRateHz"/> so the fixed-step accumulator never
/// starves, and so transport drains run faster than the sim. <c>120</c> on a 60 Hz tick
/// is a typical 2× over-clock.</param>
/// <param name="LogToFile">When true the rolling-file sink in
/// <see cref="ServerSerilogSetup"/> is enabled in addition to the console sink. Tests
/// turn this off to keep the temp directory clean.</param>
/// <param name="MatchModeId">Catalogue id of the match mode this server hosts. The
/// <see cref="ServerConsoleEntry"/> looks up the mode in the loaded
/// <c>MatchModeCatalog</c> and passes the resolved <c>MatchMode</c> to the bundle. A
/// local test server runs ONE mode per process; multi-mode hosting goes through a
/// future room registry. Default <see cref="DefaultMatchModeId"/>.</param>
public sealed record ServerConsoleOptions(
    IPEndPoint ListenEndpoint,
    int TickRateHz,
    int SnapshotIntervalTicks,
    int FramePumpHz,
    bool LogToFile,
    string MatchModeId)
{
    public string? AuthenticationKeyFilePath { get; init; }

    public string? AllowlistFilePath { get; init; }

    public string? AdminTokenFilePath { get; init; }

    public int MaxPlayers { get; init; } = 20;

    public bool AllowPublicBind { get; init; }

    public LogLevel MinimumLogLevel { get; init; } = LogLevel.Information;

    /// <summary>Default fixed-step + snapshot cadence used by runtime runs +
    /// non-overridden tests. Mirrors <c>MatchHostOptions</c> defaults; lifted here so the
    /// CLI parser has a single place to refer back to.</summary>
    public const int DefaultTickRateHz = 60;

    /// <summary>Default snapshot fan-out cadence in ticks (1 = every tick). Matches
    /// <c>MatchHostOptions.SnapshotIntervalTicks</c> Phase-32 default.</summary>
    public const int DefaultSnapshotIntervalTicks = 1;

    /// <summary>Default wall-clock frame pump frequency. 2× tick rate so the
    /// <see cref="MatchHostTickLoop"/> never under-feeds the fixed-step accumulator.</summary>
    public const int DefaultFramePumpHz = 120;

    /// <summary>Default match-mode id. Matches the first row of <c>data/match-modes.csv</c>
    /// so a server brought up with no <c>--mode</c> flag hosts the canonical
    /// 10v10 free-for-all (the prior single-mode behaviour, semantically).</summary>
    public const string DefaultMatchModeId = "hungry_battles";
}
