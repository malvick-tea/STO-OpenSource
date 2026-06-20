using System;
using System.IO;
using FluentAssertions;
using Garupan.Tools.ContentLint;
using Garupan.Tools.Tests.Fixtures;
using Xunit;

namespace Garupan.Tools.Tests.ContentLint;

/// <summary>Drives <see cref="CsvLoadRunner"/> against synthetic data trees. Real
/// loaders run inside, so the test fixtures must produce well-formed CSVs for the
/// happy path and deliberately broken CSVs for the parse-error path.</summary>
public sealed class CsvLoadRunnerTests : IDisposable
{
    private readonly TempDirectory _temp = new("sto-content-lint");
    private readonly string _root;

    public CsvLoadRunnerTests() => _root = _temp.Path;

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Missing_directory_is_flagged_separately_from_parse_errors()
    {
        var result = new CsvLoadRunner().Run(Path.Combine(_root, "does-not-exist"));

        result.DirectoryMissing.Should().BeTrue();
        result.ParseErrors.Should().BeEmpty();
        result.UnmatchedCsvFiles.Should().BeEmpty();
    }

    [Fact]
    public void Empty_data_directory_returns_clean()
    {
        var result = new CsvLoadRunner().Run(_root);

        result.DirectoryMissing.Should().BeFalse();
        result.ParseErrors.Should().BeEmpty();
        result.UnmatchedCsvFiles.Should().BeEmpty();
        result.LoadedPalette.Should().BeNull();
    }

    [Fact]
    public void Unmatched_csv_is_reported_but_does_not_error()
    {
        File.WriteAllText(Path.Combine(_root, "stray.csv"), "id,value\nfoo,1\n");

        var result = new CsvLoadRunner().Run(_root);

        result.ParseErrors.Should().BeEmpty();
        result.UnmatchedCsvFiles.Should().BeEquivalentTo("stray.csv");
    }

    [Fact]
    public void Valid_school_palette_loads_and_exposes_the_palette_for_validator()
    {
        WriteValidSchoolPalette();

        var result = new CsvLoadRunner().Run(_root);

        result.ParseErrors.Should().BeEmpty();
        result.LoadedPalette.Should().NotBeNull();
        result.LoadedPalette!.Contains(Garupan.Content.OpponentSchool.PlayerSchool).Should().BeTrue();
    }

    [Fact]
    public void Malformed_csv_is_recorded_in_parse_errors_with_relative_path()
    {
        File.WriteAllText(Path.Combine(_root, "school-palette.csv"), "this is not a valid header");

        var result = new CsvLoadRunner().Run(_root);

        result.ParseErrors.Should().ContainKey("school-palette.csv");
    }

    [Fact]
    public void Walks_subdirectories_recursively_and_dispatches_to_matchers()
    {
        Directory.CreateDirectory(Path.Combine(_root, "campaigns"));
        File.WriteAllText(Path.Combine(_root, "campaigns", "broken.csv"), "bad");

        var result = new CsvLoadRunner().Run(_root);

        result.ParseErrors.Should().ContainKey("campaigns/broken.csv");
    }

    private void WriteValidSchoolPalette()
    {
        // Minimal valid school-palette.csv: header + every OpponentSchool row at identity tint.
        var lines = new System.Collections.Generic.List<string>
        {
            "school,r,g,b,a,canon_source",
            "player_school,1.0,1.0,1.0,1.0,test fixture",
            "rival_alpha,1.0,1.0,1.0,1.0,test fixture",
            "rival_bravo,1.0,1.0,1.0,1.0,test fixture",
            "rival_charlie,1.0,1.0,1.0,1.0,test fixture",
            "rival_delta,1.0,1.0,1.0,1.0,test fixture",
            "rival_echo,1.0,1.0,1.0,1.0,test fixture",
            "rival_foxtrot,1.0,1.0,1.0,1.0,test fixture",
            "rival_golf,1.0,1.0,1.0,1.0,test fixture",
        };
        File.WriteAllLines(Path.Combine(_root, "school-palette.csv"), lines);
    }
}
