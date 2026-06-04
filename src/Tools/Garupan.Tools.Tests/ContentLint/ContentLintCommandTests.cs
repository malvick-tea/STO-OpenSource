using System;
using System.IO;
using FluentAssertions;
using Garupan.Tools.Cli;
using Garupan.Tools.ContentLint;
using Garupan.Tools.Tests.Fixtures;
using Xunit;

namespace Garupan.Tools.Tests.ContentLint;

/// <summary>End-to-end coverage of the <c>content-lint</c> subcommand. Drives the same
/// CSV fixtures the unit tests use through a real <see cref="ContentLintCommand"/> so
/// exit-code surface + argument parsing are exercised alongside the pipeline.</summary>
public sealed class ContentLintCommandTests : IDisposable
{
    private readonly TempDirectory _temp = new("sto-content-lint-cmd");
    private readonly string _dataDir;

    public ContentLintCommandTests()
    {
        _dataDir = _temp.Combine("data");
        Directory.CreateDirectory(_dataDir);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Returns_Ok_for_an_empty_data_directory()
    {
        var (exit, output, _) = RunWith("--data-dir", _dataDir);

        exit.Should().Be(CliExitCodes.Ok);
        output.Should().Contain("content-lint:");
    }

    [Fact]
    public void Returns_Failure_when_a_known_csv_is_malformed()
    {
        File.WriteAllText(Path.Combine(_dataDir, "school-palette.csv"), "not a valid header");

        var (exit, _, err) = RunWith("--data-dir", _dataDir);

        exit.Should().Be(CliExitCodes.Failure);
        err.Should().Contain("school-palette.csv");
    }

    [Fact]
    public void Returns_Failure_when_the_data_directory_does_not_exist()
    {
        var (exit, _, err) = RunWith("--data-dir", _temp.Combine("missing"));

        exit.Should().Be(CliExitCodes.Failure);
        err.Should().Contain("data directory does not exist");
    }

    [Fact]
    public void Returns_Usage_for_unknown_flag()
    {
        var (exit, _, err) = RunWith("--bogus");

        exit.Should().Be(CliExitCodes.Usage);
        err.Should().Contain("--bogus");
    }

    private (int Exit, string Output, string Error) RunWith(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var command = new ContentLintCommand();
        var exit = command.Execute(args, output, error);
        return (exit, output.ToString(), error.ToString());
    }
}
