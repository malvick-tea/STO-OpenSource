using System.Collections.Generic;
using FluentAssertions;
using Garupan.Tools.LocLint;
using Xunit;

namespace Garupan.Tools.Tests.LocLint;

/// <summary>Pure-data tests for the diff struct produced by
/// <see cref="LocLintReport.Build"/>. Drives expected / available combinations directly
/// without disk to nail the per-locale missing + orphan computation.</summary>
public sealed class LocLintReportTests
{
    [Fact]
    public void Missing_keys_are_listed_per_locale_alphabetically()
    {
        var expected = Set("a.one", "a.two", "b.three");
        var locales = new LocaleCatalogLoadResult(
            KeysByLocale: new Dictionary<string, IReadOnlyCollection<string>>(System.StringComparer.Ordinal)
            {
                ["en"] = new[] { "a.one", "a.two", "b.three" },
                ["ru"] = new[] { "a.one" },
            },
            MissingLocaleFiles: System.Array.Empty<string>());

        var report = LocLintReport.Build(expected, locales);

        report.MissingPerLocale["en"].Should().BeEmpty();
        report.MissingPerLocale["ru"].Should().Equal("a.two", "b.three");
    }

    [Fact]
    public void Orphan_keys_per_locale_are_keys_in_csv_but_not_in_expected_set()
    {
        var expected = Set("a.one");
        var locales = new LocaleCatalogLoadResult(
            KeysByLocale: new Dictionary<string, IReadOnlyCollection<string>>(System.StringComparer.Ordinal)
            {
                ["en"] = new[] { "a.one", "stray.one", "stray.two" },
            },
            MissingLocaleFiles: System.Array.Empty<string>());

        var report = LocLintReport.Build(expected, locales);

        report.OrphanPerLocale["en"].Should().Equal("stray.one", "stray.two");
        report.MissingPerLocale["en"].Should().BeEmpty();
    }

    [Fact]
    public void HasFailures_is_false_when_every_locale_covers_every_expected_key()
    {
        var expected = Set("k.a", "k.b");
        var locales = new LocaleCatalogLoadResult(
            KeysByLocale: new Dictionary<string, IReadOnlyCollection<string>>(System.StringComparer.Ordinal)
            {
                ["en"] = new[] { "k.a", "k.b" },
                ["ru"] = new[] { "k.a", "k.b" },
            },
            MissingLocaleFiles: System.Array.Empty<string>());

        LocLintReport.Build(expected, locales).HasFailures.Should().BeFalse();
    }

    [Fact]
    public void HasFailures_is_true_when_any_locale_is_missing_a_key()
    {
        var expected = Set("k.a", "k.b");
        var locales = new LocaleCatalogLoadResult(
            KeysByLocale: new Dictionary<string, IReadOnlyCollection<string>>(System.StringComparer.Ordinal)
            {
                ["en"] = new[] { "k.a", "k.b" },
                ["ru"] = new[] { "k.a" },
            },
            MissingLocaleFiles: System.Array.Empty<string>());

        LocLintReport.Build(expected, locales).HasFailures.Should().BeTrue();
    }

    [Fact]
    public void HasFailures_is_true_when_a_locale_file_is_missing_even_with_no_keys_referenced()
    {
        var locales = new LocaleCatalogLoadResult(
            KeysByLocale: new Dictionary<string, IReadOnlyCollection<string>>(System.StringComparer.Ordinal),
            MissingLocaleFiles: new[] { "ja" });

        LocLintReport.Build(Set(), locales).HasFailures.Should().BeTrue();
    }

    [Fact]
    public void Orphan_keys_do_not_flip_the_failure_gate()
    {
        var expected = Set();
        var locales = new LocaleCatalogLoadResult(
            KeysByLocale: new Dictionary<string, IReadOnlyCollection<string>>(System.StringComparer.Ordinal)
            {
                ["en"] = new[] { "stray.one" },
            },
            MissingLocaleFiles: System.Array.Empty<string>());

        LocLintReport.Build(expected, locales).HasFailures.Should().BeFalse();
    }

    private static HashSet<string> Set(params string[] keys) =>
        new(keys, System.StringComparer.Ordinal);
}
