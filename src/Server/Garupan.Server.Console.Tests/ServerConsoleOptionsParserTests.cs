using System.Net;
using FluentAssertions;
using Garupan.Server.Console;
using Xunit;

namespace Garupan.Server.Console.Tests;

public sealed class ServerConsoleOptionsParserTests
{
    [Fact]
    public void Empty_args_yield_default_loopback_options()
    {
        var result = ServerConsoleOptionsParser.Parse(System.Array.Empty<string>());

        var ok = result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Ok>().Subject;
        ok.Options.ListenEndpoint.Should().Be(new IPEndPoint(IPAddress.Loopback, ServerConsoleOptionsParser.DefaultPort));
        ok.Options.TickRateHz.Should().Be(ServerConsoleOptions.DefaultTickRateHz);
        ok.Options.SnapshotIntervalTicks.Should().Be(ServerConsoleOptions.DefaultSnapshotIntervalTicks);
        ok.Options.FramePumpHz.Should().Be(ServerConsoleOptions.DefaultFramePumpHz);
        ok.Options.LogToFile.Should().BeTrue();
    }

    [Fact]
    public void Port_flag_overrides_default()
    {
        var result = ServerConsoleOptionsParser.Parse(Args("--port", "9000"));

        var ok = result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Ok>().Subject;
        ok.Options.ListenEndpoint.Port.Should().Be(9000);
    }

    [Fact]
    public void Bind_flag_overrides_default_address()
    {
        var result = ServerConsoleOptionsParser.Parse(Args("--bind", "0.0.0.0"));

        var ok = result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Ok>().Subject;
        ok.Options.ListenEndpoint.Address.Should().Be(IPAddress.Any);
    }

    [Fact]
    public void Tick_and_snapshot_and_pump_flags_override_defaults()
    {
        var result = ServerConsoleOptionsParser.Parse(
            Args("--tick-hz", "30", "--snapshot-interval", "3", "--frame-pump-hz", "60"));

        var ok = result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Ok>().Subject;
        ok.Options.TickRateHz.Should().Be(30);
        ok.Options.SnapshotIntervalTicks.Should().Be(3);
        ok.Options.FramePumpHz.Should().Be(60);
    }

    [Fact]
    public void No_file_log_flag_disables_file_sink()
    {
        var result = ServerConsoleOptionsParser.Parse(Args("--no-file-log"));

        var ok = result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Ok>().Subject;
        ok.Options.LogToFile.Should().BeFalse();
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help_flag_returns_help_requested(string flag)
    {
        var result = ServerConsoleOptionsParser.Parse(Args(flag));

        result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.HelpRequested>();
    }

    [Fact]
    public void Unknown_flag_is_rejected_with_diagnostic()
    {
        var result = ServerConsoleOptionsParser.Parse(Args("--banana"));

        var failed = result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Failed>().Subject;
        failed.Diagnostic.Should().Contain("--banana");
    }

    [Fact]
    public void Non_numeric_port_is_treated_as_unknown_flag()
    {
        var result = ServerConsoleOptionsParser.Parse(Args("--port", "abc"));

        result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Failed>();
    }

    [Fact]
    public void Negative_port_is_rejected()
    {
        var result = ServerConsoleOptionsParser.Parse(Args("--port", "-5"));

        var failed = result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Failed>().Subject;
        failed.Diagnostic.Should().Contain("--port");
    }

    [Fact]
    public void Port_above_65535_is_rejected()
    {
        var result = ServerConsoleOptionsParser.Parse(Args("--port", "70000"));

        result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Failed>();
    }

    [Fact]
    public void Zero_tick_rate_is_rejected()
    {
        var result = ServerConsoleOptionsParser.Parse(Args("--tick-hz", "0"));

        var failed = result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Failed>().Subject;
        failed.Diagnostic.Should().Contain("--tick-hz");
    }

    [Fact]
    public void Zero_snapshot_interval_is_rejected()
    {
        var result = ServerConsoleOptionsParser.Parse(Args("--snapshot-interval", "0"));

        result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Failed>();
    }

    [Fact]
    public void Frame_pump_below_tick_rate_is_rejected()
    {
        var result = ServerConsoleOptionsParser.Parse(
            Args("--tick-hz", "60", "--frame-pump-hz", "30"));

        var failed = result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Failed>().Subject;
        failed.Diagnostic.Should().Contain("--frame-pump-hz");
    }

    [Fact]
    public void Bad_bind_address_is_rejected()
    {
        var result = ServerConsoleOptionsParser.Parse(Args("--bind", "not.an.ip"));

        result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Failed>();
    }

    [Fact]
    public void Help_text_lists_every_flag()
    {
        ServerConsoleOptionsParser.HelpText.Should().Contain("--port");
        ServerConsoleOptionsParser.HelpText.Should().Contain("--bind");
        ServerConsoleOptionsParser.HelpText.Should().Contain("--mode");
        ServerConsoleOptionsParser.HelpText.Should().Contain("--tick-hz");
        ServerConsoleOptionsParser.HelpText.Should().Contain("--snapshot-interval");
        ServerConsoleOptionsParser.HelpText.Should().Contain("--frame-pump-hz");
        ServerConsoleOptionsParser.HelpText.Should().Contain("--no-file-log");
        ServerConsoleOptionsParser.HelpText.Should().Contain("--help");
    }

    [Fact]
    public void Default_match_mode_id_is_the_canonical_free_for_all()
    {
        var result = ServerConsoleOptionsParser.Parse(System.Array.Empty<string>());

        var ok = result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Ok>().Subject;
        ok.Options.MatchModeId.Should().Be("hungry_battles", "the no-flag default mirrors the prior single-mode behaviour");
        ok.Options.MatchModeId.Should().Be(ServerConsoleOptions.DefaultMatchModeId);
    }

    [Fact]
    public void Mode_flag_overrides_default()
    {
        var result = ServerConsoleOptionsParser.Parse(Args("--mode", "tactical_5v5"));

        var ok = result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Ok>().Subject;
        ok.Options.MatchModeId.Should().Be("tactical_5v5");
    }

    [Fact]
    public void Empty_mode_value_is_rejected()
    {
        var result = ServerConsoleOptionsParser.Parse(Args("--mode", string.Empty));

        var failed = result.Should().BeOfType<ServerConsoleOptionsParser.ParseResult.Failed>().Subject;
        failed.Diagnostic.Should().Contain("--mode");
    }

    private static string[] Args(params string[] flags) => flags;
}
