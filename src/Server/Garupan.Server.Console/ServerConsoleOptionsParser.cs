using System;
using System.Globalization;
using System.Net;
using Garupan.Sim.Snapshot;
using Microsoft.Extensions.Logging;

namespace Garupan.Server.Console;

/// <summary>
/// Maps the command-line arguments handed to <c>Garupan.Server.Console</c> into a
/// resolved <see cref="ServerConsoleOptions"/>. Pure-CPU + headless-testable — accepts a
/// plain <c>string[]</c>, returns either <see cref="ParseResult.Ok"/> with the options or
/// <see cref="ParseResult.Failed"/> with a one-line diagnostic. No <c>Environment.Exit</c>,
/// no console writes — those belong to <see cref="Program.Main"/>.
/// </summary>
/// <remarks>
/// <para>
/// CLI surface: <c>--port N</c>, <c>--bind X.X.X.X</c>, <c>--tick-hz N</c>,
/// <c>--snapshot-interval N</c>, <c>--frame-pump-hz N</c>, <c>--no-file-log</c>,
/// <c>--help</c>. Every flag has a sensible default — invoking with zero args binds to
/// <c>127.0.0.1:7777</c> at 60 Hz / 1-tick snapshots / 120 Hz pump / rolling file logs on.
/// </para>
/// </remarks>
public static class ServerConsoleOptionsParser
{
    private const int MaximumTickRateHz = 1000;
    private const int MaximumFramePumpHz = 1000;

    /// <summary>Default UDP port the server binds to when none is supplied — picked to
    /// avoid the IANA well-known range while staying high enough for a non-root user on
    /// any OS to bind. Override on the CLI for runtime deployments.</summary>
    public const int DefaultPort = 7777;

    /// <summary>Multi-line block describing every flag. Surfaced to stdout when the user
    /// passes <c>--help</c>.</summary>
    public static string HelpText { get; } = string.Join(Environment.NewLine, new[]
    {
        "STO server console - authoritative match server",
        string.Empty,
        "Flags:",
        "  --port N               UDP port to bind to. Default: 7777. Use 0 for an OS-assigned ephemeral.",
        "  --bind ADDRESS         IPv4 address to bind on. Default: 127.0.0.1.",
        "  --mode ID              Match mode catalogue id this server hosts.",
        "                         Default: hungry_battles. Known local test modes:",
        "                           hungry_battles  10v10 free-for-all, 3 respawns.",
        "                           tactical_5v5    5v5 team play, 1 respawn.",
        "  --tick-hz N            Authoritative sim tick rate. Default: 60.",
        "  --snapshot-interval N  Broadcast a snapshot every N ticks. Default: 1.",
        "  --frame-pump-hz N      Wall-clock frame pump frequency (>= tick-hz). Default: 120.",
        "  --auth-key-file PATH   Required key file for authenticated UDP sessions.",
        "  --allowlist-file PATH  Optional UTF-8 file containing one allowed IPv4 address per line.",
        "  --admin-token-file PATH Enable authenticated local-console admin commands.",
        "  --max-players N        Maximum authenticated players admitted. Default: 20.",
        "  --public               Explicitly allow a non-loopback bind address.",
        "  --log-level LEVEL      trace, debug, information, warning, error, or critical.",
        "  --no-file-log          Disable the rolling file log sink (console-only logging).",
        "  --help, -h             Print this message and exit.",
    });

    public static ParseResult Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var port = DefaultPort;
        var address = IPAddress.Loopback;
        var tickHz = ServerConsoleOptions.DefaultTickRateHz;
        var snapshotInterval = ServerConsoleOptions.DefaultSnapshotIntervalTicks;
        var framePumpHz = ServerConsoleOptions.DefaultFramePumpHz;
        var logToFile = true;
        var matchModeId = ServerConsoleOptions.DefaultMatchModeId;
        string? authenticationKeyFilePath = null;
        string? allowlistFilePath = null;
        string? adminTokenFilePath = null;
        var maxPlayers = 20;
        var allowPublicBind = false;
        var minimumLogLevel = LogLevel.Information;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    return new ParseResult.HelpRequested();
                case "--no-file-log":
                    logToFile = false;
                    break;
                case "--port" when TryReadInt(args, ref i, out var v):
                    port = v;
                    break;
                case "--bind" when TryReadString(args, ref i, out var s):
                    if (!IPAddress.TryParse(s, out var parsedAddress))
                    {
                        return new ParseResult.Failed($"--bind expects an IPv4 address, got '{s}'");
                    }

                    address = parsedAddress;
                    break;
                case "--mode" when TryReadString(args, ref i, out var s):
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        return new ParseResult.Failed("--mode expects a non-empty mode id");
                    }

                    matchModeId = s;
                    break;
                case "--tick-hz" when TryReadInt(args, ref i, out var v):
                    tickHz = v;
                    break;
                case "--snapshot-interval" when TryReadInt(args, ref i, out var v):
                    snapshotInterval = v;
                    break;
                case "--frame-pump-hz" when TryReadInt(args, ref i, out var v):
                    framePumpHz = v;
                    break;
                case "--auth-key-file" when TryReadString(args, ref i, out var s):
                    authenticationKeyFilePath = s;
                    break;
                case "--allowlist-file" when TryReadString(args, ref i, out var s):
                    allowlistFilePath = s;
                    break;
                case "--admin-token-file" when TryReadString(args, ref i, out var s):
                    adminTokenFilePath = s;
                    break;
                case "--max-players" when TryReadInt(args, ref i, out var v):
                    maxPlayers = v;
                    break;
                case "--public":
                    allowPublicBind = true;
                    break;
                case "--log-level" when TryReadString(args, ref i, out var s):
                    if (!Enum.TryParse<LogLevel>(s, ignoreCase: true, out minimumLogLevel)
                        || minimumLogLevel == LogLevel.None)
                    {
                        return new ParseResult.Failed(
                            $"--log-level has unsupported value '{s}'");
                    }

                    break;
                default:
                    return new ParseResult.Failed($"unrecognised argument: '{arg}'");
            }
        }

        if (port < 0 || port > 65535)
        {
            return new ParseResult.Failed($"--port must be in [0,65535], got {port}");
        }

        if (tickHz is <= 0 or > MaximumTickRateHz)
        {
            return new ParseResult.Failed(
                $"--tick-hz must be in [1,{MaximumTickRateHz}], got {tickHz}");
        }

        if (snapshotInterval <= 0)
        {
            return new ParseResult.Failed($"--snapshot-interval must be positive, got {snapshotInterval}");
        }

        if (framePumpHz < tickHz)
        {
            return new ParseResult.Failed(
                $"--frame-pump-hz ({framePumpHz}) must be >= --tick-hz ({tickHz}) so the sim's fixed-step accumulator never starves");
        }

        if (framePumpHz > MaximumFramePumpHz)
        {
            return new ParseResult.Failed(
                $"--frame-pump-hz must be at most {MaximumFramePumpHz}, got {framePumpHz}");
        }

        if (maxPlayers is <= 0 or > SnapshotWire.MaxEntities)
        {
            return new ParseResult.Failed(
                $"--max-players must be in [1,{SnapshotWire.MaxEntities}], got {maxPlayers}");
        }

        if (!IPAddress.IsLoopback(address) && !allowPublicBind)
        {
            return new ParseResult.Failed(
                "a non-loopback --bind requires the explicit --public acknowledgement");
        }

        return new ParseResult.Ok(new ServerConsoleOptions(
            new IPEndPoint(address, port),
            tickHz,
            snapshotInterval,
            framePumpHz,
            logToFile,
            matchModeId)
        {
            AuthenticationKeyFilePath = authenticationKeyFilePath,
            AllowlistFilePath = allowlistFilePath,
            AdminTokenFilePath = adminTokenFilePath,
            MaxPlayers = maxPlayers,
            AllowPublicBind = allowPublicBind,
            MinimumLogLevel = minimumLogLevel,
        });
    }

    private static bool TryReadString(string[] args, ref int index, out string value)
    {
        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    private static bool TryReadInt(string[] args, ref int index, out int value)
    {
        value = 0;
        if (!TryReadString(args, ref index, out var raw))
        {
            return false;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Outcome of a parse pass — either fully resolved options or a usage error
    /// the caller surfaces as a non-zero exit code.</summary>
    public abstract record ParseResult
    {
        public sealed record Ok(ServerConsoleOptions Options) : ParseResult;

        public sealed record Failed(string Diagnostic) : ParseResult;

        public sealed record HelpRequested : ParseResult;
    }
}
