using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Garupan.Tools.Cli;
using Xunit;

namespace Garupan.Tools.Tests.Cli;

/// <summary>Coverage for the top-level CLI dispatcher: argument routing, help output,
/// unknown subcommand handling. Drives a stub <see cref="ICommand"/> so the test does
/// not depend on any concrete subcommand's behaviour.</summary>
public sealed class CommandDispatcherTests
{
    [Fact]
    public void No_args_prints_usage_and_returns_Usage_exit_code()
    {
        var dispatcher = new CommandDispatcher(new[] { new StubCommand("alpha") });

        var (exit, output, _) = RunWith(dispatcher);

        exit.Should().Be(CliExitCodes.Usage);
        output.Should().Contain("sto-tools").And.Contain("alpha");
    }

    [Fact]
    public void Help_flag_prints_usage_and_returns_Ok()
    {
        var dispatcher = new CommandDispatcher(new[] { new StubCommand("alpha") });

        var (exit, output, _) = RunWith(dispatcher, "--help");

        exit.Should().Be(CliExitCodes.Ok);
        output.Should().Contain("subcommands:");
    }

    [Fact]
    public void Unknown_subcommand_returns_Usage_with_error_message()
    {
        var dispatcher = new CommandDispatcher(new[] { new StubCommand("alpha") });

        var (exit, _, err) = RunWith(dispatcher, "missing");

        exit.Should().Be(CliExitCodes.Usage);
        err.Should().Contain("Unknown subcommand 'missing'.");
    }

    [Fact]
    public void Dispatches_remaining_args_to_the_matching_command()
    {
        var stub = new StubCommand("alpha");
        var dispatcher = new CommandDispatcher(new[] { stub });

        RunWith(dispatcher, "alpha", "--foo", "bar");

        stub.ReceivedArgs.Should().Equal("--foo", "bar");
    }

    [Fact]
    public void Returns_the_commands_exit_code_verbatim()
    {
        var dispatcher = new CommandDispatcher(new[] { new StubCommand("alpha", exitCode: 7) });

        var (exit, _, _) = RunWith(dispatcher, "alpha");

        exit.Should().Be(7);
    }

    private static (int Exit, string Output, string Error) RunWith(CommandDispatcher dispatcher, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = dispatcher.Run(args, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    private sealed class StubCommand : ICommand
    {
        public StubCommand(string name, int exitCode = 0)
        {
            Name = name;
            ExitCode = exitCode;
        }

        public string Name { get; }

        public string Description => "stub";

        public int ExitCode { get; }

        public IReadOnlyList<string> ReceivedArgs { get; private set; } = Array.Empty<string>();

        public int Execute(IReadOnlyList<string> args, TextWriter output, TextWriter error)
        {
            ReceivedArgs = args;
            return ExitCode;
        }
    }
}
