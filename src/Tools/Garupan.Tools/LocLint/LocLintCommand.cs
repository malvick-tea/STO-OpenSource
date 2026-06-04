using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Garupan.Localisation;
using Garupan.Tools.Cli;

namespace Garupan.Tools.LocLint;

/// <summary>Top-level <c>loc-lint</c> subcommand. Verifies that every translation key
/// referenced from <see cref="L10nKeys"/> and the data CSVs is present in every
/// requested locale catalog. Wraps the three sources (registry, data refs, locale
/// CSVs) and renders the diff. Exit-code surface lives in <see cref="CliExitCodes"/>.</summary>
internal sealed class LocLintCommand : ICommand
{
    private readonly KeyRegistryScanner _registryScanner;
    private readonly DataCsvKeyScanner _dataScanner;
    private readonly LocaleCatalogLoader _localeLoader;
    private readonly LocLintReporter _reporter;

    public LocLintCommand()
        : this(new KeyRegistryScanner(), new DataCsvKeyScanner(), new LocaleCatalogLoader(), new LocLintReporter())
    {
    }

    internal LocLintCommand(
        KeyRegistryScanner registryScanner,
        DataCsvKeyScanner dataScanner,
        LocaleCatalogLoader localeLoader,
        LocLintReporter reporter)
    {
        ArgumentNullException.ThrowIfNull(registryScanner);
        ArgumentNullException.ThrowIfNull(dataScanner);
        ArgumentNullException.ThrowIfNull(localeLoader);
        ArgumentNullException.ThrowIfNull(reporter);
        _registryScanner = registryScanner;
        _dataScanner = dataScanner;
        _localeLoader = localeLoader;
        _reporter = reporter;
    }

    public string Name => "loc-lint";

    public string Description => "verify localization CSV coverage of every referenced translation key";

    public int Execute(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        LocLintOptions options;
        try
        {
            options = LocLintOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            error.WriteLine($"ERROR: {ex.Message}");
            error.WriteLine("usage: sto-tools loc-lint [--locale-dir <path>] [--data-dir <path>] [--locales en,ru,ja]");
            return CliExitCodes.Usage;
        }

        var expected = CollectExpectedKeys(options);
        var localeResult = _localeLoader.Load(options.LocaleDirectory, options.Locales);
        var report = LocLintReport.Build(expected, localeResult);
        _reporter.Render(report, output, error);
        return report.HasFailures ? CliExitCodes.Failure : CliExitCodes.Ok;
    }

    private IReadOnlyCollection<string> CollectExpectedKeys(LocLintOptions options)
    {
        var registryKeys = _registryScanner.Collect(typeof(L10nKeys));
        var dataKeys = _dataScanner.Collect(options.DataDirectory);
        return new HashSet<string>(registryKeys.Concat(dataKeys), StringComparer.Ordinal);
    }
}
