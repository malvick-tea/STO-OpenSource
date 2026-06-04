using System.Collections.Generic;
using FluentAssertions;
using Garupan.Tools.ContentLint;
using Xunit;

namespace Garupan.Tools.Tests.ContentLint;

public sealed class ContentLintReportTests
{
    [Fact]
    public void HasFailures_is_false_when_no_errors_and_no_missing_directory()
    {
        var result = new CsvLoadResult(
            new Dictionary<string, string>(StringComparer.Ordinal),
            System.Array.Empty<string>(),
            LoadedPalette: null,
            DirectoryMissing: false);

        ContentLintReport.From(result).HasFailures.Should().BeFalse();
    }

    [Fact]
    public void HasFailures_is_true_when_directory_missing()
    {
        var result = new CsvLoadResult(
            new Dictionary<string, string>(StringComparer.Ordinal),
            System.Array.Empty<string>(),
            LoadedPalette: null,
            DirectoryMissing: true);

        ContentLintReport.From(result).HasFailures.Should().BeTrue();
    }

    [Fact]
    public void HasFailures_is_true_when_any_parse_error_present()
    {
        var result = new CsvLoadResult(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["bad.csv"] = "boom" },
            System.Array.Empty<string>(),
            LoadedPalette: null,
            DirectoryMissing: false);

        ContentLintReport.From(result).HasFailures.Should().BeTrue();
    }

    [Fact]
    public void Unmatched_csvs_alone_do_not_flip_failure_gate()
    {
        var result = new CsvLoadResult(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new[] { "stray.csv" },
            LoadedPalette: null,
            DirectoryMissing: false);

        ContentLintReport.From(result).HasFailures.Should().BeFalse();
    }
}
