using System;
using System.IO;
using FluentAssertions;
using Garupan.Tools.LocLint;
using Garupan.Tools.Tests.Fixtures;
using Xunit;

namespace Garupan.Tools.Tests.LocLint;

/// <summary>Behaviour of <see cref="LocaleCatalogLoader"/>: reads CSVs via the same
/// <c>CsvCatalog</c> the runtime uses, surfaces missing-file locales separately. Tests
/// drive real disk because the loader is intentionally <see cref="File"/>-based.</summary>
public sealed class LocaleCatalogLoaderTests : IDisposable
{
    private readonly TempDirectory _temp = new("sto-locloader");
    private readonly string _root;

    public LocaleCatalogLoaderTests() => _root = _temp.Path;

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Load_reads_every_requested_locale_csv()
    {
        WriteCsv("en.csv", "key,text", "common.ok,OK", "menu.title,STO");
        WriteCsv("ru.csv", "key,text", "common.ok,ОК", "menu.title,STO");

        var result = new LocaleCatalogLoader().Load(_root, new[] { "en", "ru" });

        result.KeysByLocale.Should().HaveCount(2);
        result.KeysByLocale["en"].Should().BeEquivalentTo(new[] { "common.ok", "menu.title" });
        result.KeysByLocale["ru"].Should().BeEquivalentTo(new[] { "common.ok", "menu.title" });
        result.MissingLocaleFiles.Should().BeEmpty();
    }

    [Fact]
    public void Load_reports_missing_locale_files_separately()
    {
        WriteCsv("en.csv", "key,text", "k.a,A");

        var result = new LocaleCatalogLoader().Load(_root, new[] { "en", "ru", "ja" });

        result.KeysByLocale.Should().HaveCount(1);
        result.KeysByLocale.Should().ContainKey("en");
        result.MissingLocaleFiles.Should().BeEquivalentTo(new[] { "ru", "ja" });
    }

    [Fact]
    public void Load_returns_empty_keys_for_a_catalog_with_only_a_header()
    {
        WriteCsv("en.csv", "key,text");

        var result = new LocaleCatalogLoader().Load(_root, new[] { "en" });

        result.KeysByLocale["en"].Should().BeEmpty();
        result.MissingLocaleFiles.Should().BeEmpty();
    }

    private void WriteCsv(string name, params string[] lines) =>
        File.WriteAllLines(Path.Combine(_root, name), lines);
}
