using System;
using System.Collections.Generic;
using System.IO;
using Garupan.Tools.Cli;

namespace Garupan.Tools.ContentLint;

/// <summary>Top-level <c>content-lint</c> subcommand. Walks <c>data/</c>, runs each
/// known authoring CSV through its actual runtime loader, then runs the
/// <see cref="Garupan.Content.CatalogValidator"/> against the loaded
/// <see cref="Garupan.Content.SchoolPalette"/>. Failures exit non-zero; unmatched CSVs
/// emit a warning but stay green so a CSV authored ahead of its loader doesn't break
/// CI.</summary>
internal sealed class ContentLintCommand : ICommand
{
    private readonly CsvLoadRunner _runner;
    private readonly ContentLintReporter _reporter;

    public ContentLintCommand()
        : this(new CsvLoadRunner(), new ContentLintReporter())
    {
    }

    internal ContentLintCommand(CsvLoadRunner runner, ContentLintReporter reporter)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(reporter);
        _runner = runner;
        _reporter = reporter;
    }

    public string Name => "content-lint";

    public string Description => "parse every data/*.csv via the runtime loader + run the catalog validator";

    public int Execute(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        ContentLintOptions options;
        try
        {
            options = ContentLintOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            error.WriteLine($"ERROR: {ex.Message}");
            error.WriteLine("usage: sto-tools content-lint [--data-dir <path>]");
            return CliExitCodes.Usage;
        }

        var load = _runner.Run(options.DataDirectory);
        var report = ContentLintReport.From(load);
        _reporter.Render(report, output, error);
        return report.HasFailures ? CliExitCodes.Failure : CliExitCodes.Ok;
    }
}
