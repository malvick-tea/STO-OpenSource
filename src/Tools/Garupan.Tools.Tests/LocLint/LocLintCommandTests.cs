using System;
using System.IO;
using FluentAssertions;
using Garupan.Tools.Cli;
using Garupan.Tools.LocLint;
using Garupan.Tools.Tests.Fixtures;
using Xunit;

namespace Garupan.Tools.Tests.LocLint;

/// <summary>End-to-end coverage of the <c>loc-lint</c> subcommand. Drives real temp
/// directories so the full pipeline (registry scan → data scan → locale load →
/// report → render → exit code) runs as a unit; smaller pieces have their own focused
/// tests above.</summary>
public sealed class LocLintCommandTests : IDisposable
{
    private readonly TempDirectory _temp = new("sto-loclint-cmd");
    private readonly string _localeDir;
    private readonly string _dataDir;

    public LocLintCommandTests()
    {
        _localeDir = _temp.Combine("localization");
        _dataDir = _temp.Combine("data");
        Directory.CreateDirectory(_localeDir);
        Directory.CreateDirectory(_dataDir);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Returns_Failure_when_a_referenced_key_is_missing_from_a_locale()
    {
        // Write an `en` catalog with the canonical L10nKeys covered but `ru` missing one.
        WriteLocale("en", "common.ok,OK", "menu.title,STO");
        WriteLocale("ru", "common.ok,ОК");

        var (exit, _, errOutput) = RunWith("--locale-dir", _localeDir, "--data-dir", _dataDir, "--locales", "en,ru");

        exit.Should().Be(CliExitCodes.Failure);
        errOutput.Should().Contain("ru").And.Contain("menu.title");
    }

    [Fact]
    public void Returns_Failure_when_a_locale_file_is_missing()
    {
        WriteLocale("en", "common.ok,OK");

        var (exit, _, errOutput) = RunWith("--locale-dir", _localeDir, "--data-dir", _dataDir, "--locales", "en,ru");

        exit.Should().Be(CliExitCodes.Failure);
        errOutput.Should().Contain("ru");
    }

    [Fact]
    public void Returns_Usage_when_argument_is_unknown()
    {
        var (exit, _, errOutput) = RunWith("--bogus");

        exit.Should().Be(CliExitCodes.Usage);
        errOutput.Should().Contain("--bogus");
    }

    [Fact]
    public void Includes_data_csv_keys_in_the_expected_set()
    {
        // Cover every L10nKeys entry so failure is driven solely by the data CSV reference.
        WriteCompleteLocale("en");
        WriteCompleteLocale("ru");
        WriteCompleteLocale("ja");
        File.WriteAllLines(Path.Combine(_dataDir, "mini.csv"), new[]
        {
            "id,title_key",
            "m1,data.referenced.only",
        });

        var (exit, _, errOutput) = RunWith("--locale-dir", _localeDir, "--data-dir", _dataDir, "--locales", "en,ru,ja");

        exit.Should().Be(CliExitCodes.Failure);
        errOutput.Should().Contain("data.referenced.only");
    }

    [Fact]
    public void Exit_code_is_Ok_when_every_referenced_key_is_present()
    {
        WriteCompleteLocale("en");

        var (exit, output, _) = RunWith("--locale-dir", _localeDir, "--data-dir", _dataDir, "--locales", "en");

        exit.Should().Be(CliExitCodes.Ok);
        output.Should().Contain("loc-lint:");
    }

    private (int Exit, string Output, string Error) RunWith(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var command = new LocLintCommand();
        var exit = command.Execute(args, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    private void WriteLocale(string locale, params string[] entries)
    {
        var lines = new string[entries.Length + 1];
        lines[0] = "key,text";
        Array.Copy(entries, 0, lines, 1, entries.Length);
        File.WriteAllLines(Path.Combine(_localeDir, $"{locale}.csv"), lines);
    }

    private void WriteCompleteLocale(string locale)
    {
        // Dump every L10nKeys entry into the locale file via the same scanner the lint uses.
        var keys = new KeyRegistryScanner().Collect(typeof(Garupan.Localisation.L10nKeys));
        var lines = new System.Collections.Generic.List<string> { "key,text" };
        foreach (var key in keys)
        {
            lines.Add($"{key},{locale}:{key}");
        }

        File.WriteAllLines(Path.Combine(_localeDir, $"{locale}.csv"), lines);
    }
}
