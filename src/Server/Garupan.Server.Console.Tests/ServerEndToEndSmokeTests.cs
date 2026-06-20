using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Garupan.Content;
using Garupan.Net.Session;
using Garupan.Server.Console;
using Garupan.Sim.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Opus.Net.Udp.Transport;
using Xunit;

namespace Garupan.Server.Console.Tests;

/// <summary>
/// Boots the full <c>Garupan.Server.Console</c> composition (UdpServerTransport +
/// MatchHost + tick loop) on an ephemeral loopback port and connects a real
/// <see cref="UdpClientTransport"/> + <see cref="ClientSession"/> against it. Proves the
/// runtime artifact behaves as a runnable server: a client gets Welcome with a
/// network-id, snapshots fan out, clean shutdown surfaces a Disconnect on the client.
/// </summary>
[Collection(nameof(ServerEndToEndSmokeTests))]
[CollectionDefinition(nameof(ServerEndToEndSmokeTests), DisableParallelization = true)]
public sealed class ServerEndToEndSmokeTests
{
    private static readonly TimeSpan EventWaitBudget = TimeSpan.FromSeconds(4);
    private static readonly byte[] TestAuthenticationKey = RandomNumberGenerator.GetBytes(32);

    private static readonly UdpTransportOptions FastTransportOptions = new()
    {
        HeartbeatInterval = TimeSpan.FromMilliseconds(80),
        DeadlineDuration = TimeSpan.FromMilliseconds(800),
        ReceivePollInterval = TimeSpan.FromMilliseconds(40),
        ConnectTimeout = TimeSpan.FromSeconds(2),
        AuthenticationKey = TestAuthenticationKey,
    };

    private static readonly string[] BadArgs = { "--unknown-flag" };

    private static readonly string[] HelpArgs = { "--help" };

    private static readonly MatchModeCatalog TestCatalog = MatchModeCsv.Parse(
        """
        id,kind,name_key,summary_key,lobby_capacity,respawn_limit,commander_led
        hungry_battles,FreeForAll,lobby.mode.hungry.name,lobby.mode.hungry.summary,20,3,false
        tactical_5v5,TeamTactical,lobby.mode.tactical.name,lobby.mode.tactical.summary,10,1,true
        """);

    [Fact]
    public async Task Connected_client_receives_welcome_and_snapshot()
    {
        using var authenticationKeyFile = TemporaryAuthenticationKeyFile.Create(TestAuthenticationKey);
        var options = new ServerConsoleOptions(
            ListenEndpoint: new IPEndPoint(IPAddress.Loopback, 0),
            TickRateHz: 60,
            SnapshotIntervalTicks: 1,
            FramePumpHz: 240,
            LogToFile: false,
            MatchModeId: "hungry_battles")
        {
            AuthenticationKeyFilePath = authenticationKeyFile.Path,
        };

        using var bundle = ServerHostBundle.Create(
            options,
            TestCatalog.Find("hungry_battles")!,
            NullLoggerFactory.Instance,
            FastTransportOptions);
        using var cts = new CancellationTokenSource();
        var loopTask = Task.Run(() => bundle.TickLoop.Run(cts.Token));

        try
        {
            using var clientTransport = new UdpClientTransport(
                "smoke-client",
                bundle.BoundEndpoint,
                FastTransportOptions);
            using var clientSession = new ClientSession(clientTransport);

            var welcomes = new List<WelcomeFrame>();
            var snapshots = new List<int>();
            clientSession.WelcomeReceived += w => welcomes.Add(w);
            clientSession.SnapshotReceived += s => snapshots.Add(s.Entities.Count);

            await PumpUntil(clientSession, () => welcomes.Count > 0, EventWaitBudget);
            await PumpUntil(clientSession, () => snapshots.Count > 0, EventWaitBudget);

            welcomes.Should().HaveCountGreaterThan(0, "the server must send Welcome on Connected");
            welcomes[0].NetworkId.Should().BeGreaterThan(0u, "Welcome must carry a non-zero network id");
            snapshots.Should().HaveCountGreaterThan(0, "the server's snapshot broadcast must reach the client");
            snapshots[0].Should().Be(1, "the snapshot must include the one spawned tank");
            bundle.Host.PlayerCount.Should().Be(1, "the host must have registered our peer");
        }
        finally
        {
            cts.Cancel();
            await loopTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task Server_console_entry_run_returns_clean_on_cancellation()
    {
        using var authenticationKeyFile = TemporaryAuthenticationKeyFile.Create(TestAuthenticationKey);
        using var shutdown = new ShutdownSignal();
        var runTask = Task.Run(() => ServerConsoleEntry.Run(
            RuntimeArgs(authenticationKeyFile.Path),
            TestCatalog,
            NullLoggerFactory.Instance,
            shutdown));

        await Task.Delay(200);
        shutdown.Signal();

        var finished = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(3)));
        finished.Should().Be(runTask);
        var exitCode = await runTask;
        exitCode.Should().Be(ServerConsoleEntry.ExitOk);
    }

    [Fact]
    public void Server_console_entry_run_returns_options_error_on_bad_args()
    {
        using var shutdown = new ShutdownSignal();

        var exitCode = ServerConsoleEntry.Run(BadArgs, TestCatalog, NullLoggerFactory.Instance, shutdown);

        exitCode.Should().Be(ServerConsoleEntry.ExitOptionsError);
    }

    [Fact]
    public void Server_console_entry_run_returns_help_when_help_flag_present()
    {
        using var shutdown = new ShutdownSignal();

        var exitCode = ServerConsoleEntry.Run(HelpArgs, TestCatalog, NullLoggerFactory.Instance, shutdown);

        exitCode.Should().Be(ServerConsoleEntry.ExitHelp);
    }

    [Fact]
    public async Task Server_console_entry_run_returns_bind_error_when_port_in_use()
    {
        using var authenticationKeyFile = TemporaryAuthenticationKeyFile.Create(TestAuthenticationKey);
        using var first = UdpServerTransport.Bind(
            "first",
            new IPEndPoint(IPAddress.Loopback, 0),
            FastTransportOptions);
        var portInUse = first.BoundEndpoint.Port;

        using var shutdown = new ShutdownSignal();
        var runTask = Task.Run(() => ServerConsoleEntry.Run(
            BindCollisionArgs(portInUse, authenticationKeyFile.Path),
            TestCatalog,
            NullLoggerFactory.Instance,
            shutdown));

        var finished = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(3)));
        finished.Should().Be(runTask, "the run must fail fast on bind, not hang");
        (await runTask).Should().Be(ServerConsoleEntry.ExitBindError);
    }

    [Fact]
    public void Server_console_entry_run_returns_catalog_error_on_unknown_mode()
    {
        using var shutdown = new ShutdownSignal();
        var args = new[] { "--mode", "no_such_mode", "--port", "0", "--no-file-log" };

        var exitCode = ServerConsoleEntry.Run(args, TestCatalog, NullLoggerFactory.Instance, shutdown);

        exitCode.Should().Be(ServerConsoleEntry.ExitCatalogError);
    }

    [Fact]
    public async Task Server_console_entry_run_accepts_known_tactical_mode()
    {
        using var authenticationKeyFile = TemporaryAuthenticationKeyFile.Create(TestAuthenticationKey);
        using var shutdown = new ShutdownSignal();
        var args = RuntimeArgs(authenticationKeyFile.Path, "--mode", "tactical_5v5");
        var runTask = Task.Run(() => ServerConsoleEntry.Run(
            args,
            TestCatalog,
            NullLoggerFactory.Instance,
            shutdown));

        await Task.Delay(200);
        shutdown.Signal();

        var finished = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(3)));
        finished.Should().Be(runTask);
        (await runTask).Should().Be(ServerConsoleEntry.ExitOk);
    }

    private static string[] BindCollisionArgs(int port, string authenticationKeyFilePath) => new[]
    {
        "--port",
        port.ToString(CultureInfo.InvariantCulture),
        "--no-file-log",
        "--auth-key-file",
        authenticationKeyFilePath,
    };

    private static string[] RuntimeArgs(string authenticationKeyFilePath, params string[] additionalArgs)
    {
        var args = new List<string>
        {
            "--port",
            "0",
            "--no-file-log",
            "--frame-pump-hz",
            "240",
            "--auth-key-file",
            authenticationKeyFilePath,
        };
        args.AddRange(additionalArgs);
        return args.ToArray();
    }

    private static async Task PumpUntil(ClientSession session, Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            session.Pump();
            if (predicate())
            {
                return;
            }

            await Task.Delay(20);
        }

        session.Pump();
    }

    private sealed class TemporaryAuthenticationKeyFile : IDisposable
    {
        private TemporaryAuthenticationKeyFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryAuthenticationKeyFile Create(ReadOnlySpan<byte> key)
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"garupan-auth-{Guid.NewGuid():N}.key");
            File.WriteAllText(path, Convert.ToBase64String(key));
            return new TemporaryAuthenticationKeyFile(path);
        }

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}
