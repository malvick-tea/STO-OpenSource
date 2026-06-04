using System;
using System.IO;
using FluentAssertions;
using Garupan.Tools.LocLint;
using Garupan.Tools.Tests.Fixtures;
using Xunit;

namespace Garupan.Tools.Tests.LocLint;

/// <summary>Behaviour of the data-CSV scanner that pulls translation keys out of any
/// column whose header ends with <c>_key</c>. Drives a temp directory with a handful
/// of CSV fixtures, then asserts the deduplicated key list.</summary>
public sealed class DataCsvKeyScannerTests : IDisposable
{
    private readonly TempDirectory _temp = new("sto-loclint");
    private readonly string _root;

    public DataCsvKeyScannerTests() => _root = _temp.Path;

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Empty_directory_returns_empty_set()
    {
        new DataCsvKeyScanner().Collect(_root).Should().BeEmpty();
    }

    [Fact]
    public void Non_existent_directory_returns_empty_set()
    {
        var missing = Path.Combine(_root, "does-not-exist");
        new DataCsvKeyScanner().Collect(missing).Should().BeEmpty();
    }

    [Fact]
    public void Collects_keys_from_columns_ending_in_underscore_key()
    {
        WriteFile(
            "a.csv",
            "id,title_key,briefing_key,score",
            "m1,m1.title,m1.briefing,42",
            "m2,m2.title,m2.briefing,99");

        var keys = new DataCsvKeyScanner().Collect(_root);

        keys.Should().BeEquivalentTo(new[] { "m1.title", "m1.briefing", "m2.title", "m2.briefing" });
    }

    [Fact]
    public void Ignores_columns_without_the_underscore_key_suffix()
    {
        WriteFile(
            "b.csv",
            "id,name,school",
            "p1,the player commander,PlayerSchool");

        new DataCsvKeyScanner().Collect(_root).Should().BeEmpty();
    }

    [Fact]
    public void Skips_blank_values_in_key_columns()
    {
        WriteFile(
            "c.csv",
            "id,lore_key",
            "p1,",
            "p2,    ",
            "p3,real.key");

        new DataCsvKeyScanner().Collect(_root).Should().BeEquivalentTo(new[] { "real.key" });
    }

    [Fact]
    public void Splits_pipe_separated_multi_key_cells()
    {
        WriteFile(
            "d.csv",
            "id,prereq_key",
            "m1,a.key|b.key|c.key");

        new DataCsvKeyScanner().Collect(_root).Should().BeEquivalentTo(new[] { "a.key", "b.key", "c.key" });
    }

    [Fact]
    public void Skips_comment_and_blank_lines()
    {
        WriteFile(
            "e.csv",
            "# this is a comment",
            string.Empty,
            "id,title_key",
            "# inline comment",
            "m1,m1.title");

        new DataCsvKeyScanner().Collect(_root).Should().BeEquivalentTo(new[] { "m1.title" });
    }

    [Fact]
    public void Walks_subdirectories_recursively()
    {
        var sub = Path.Combine(_root, "campaigns");
        Directory.CreateDirectory(sub);
        WriteFileAt(sub, "sample.csv",
            "id,briefing_key",
            "m1,sample.brief.one");

        new DataCsvKeyScanner().Collect(_root).Should().BeEquivalentTo(new[] { "sample.brief.one" });
    }

    [Fact]
    public void Header_matching_is_case_insensitive()
    {
        WriteFile(
            "f.csv",
            "id,TITLE_KEY,Lore_Key",
            "m1,upper.title,mixed.lore");

        new DataCsvKeyScanner().Collect(_root).Should().BeEquivalentTo(new[] { "upper.title", "mixed.lore" });
    }

    [Fact]
    public void Skips_non_dotted_values_treated_as_short_identifiers()
    {
        // school_key=player_school is a school-registry id, not a translation key.
        // Translation keys are dotted (e.g. school.player_school); non-dotted values are skipped.
        WriteFile(
            "h.csv",
            "id,school_key",
            "p1,player_school",
            "p2,rival_echo",
            "p3,school.player_school");

        new DataCsvKeyScanner().Collect(_root).Should().BeEquivalentTo(new[] { "school.player_school" });
    }

    [Fact]
    public void Deduplicates_keys_across_files()
    {
        WriteFile("g1.csv", "id,title_key", "m1,shared.key");
        WriteFile("g2.csv", "id,title_key", "m2,shared.key");

        new DataCsvKeyScanner().Collect(_root).Should().BeEquivalentTo(new[] { "shared.key" });
    }

    private void WriteFile(string name, params string[] lines) =>
        WriteFileAt(_root, name, lines);

    private static void WriteFileAt(string directory, string name, params string[] lines) =>
        File.WriteAllLines(Path.Combine(directory, name), lines);
}
