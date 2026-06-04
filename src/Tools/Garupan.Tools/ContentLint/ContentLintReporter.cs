using System;
using System.IO;

namespace Garupan.Tools.ContentLint;

/// <summary>Renders a <see cref="ContentLintReport"/> to text. Pure formatter — the
/// command pipes <c>Console.Out</c> / <c>Console.Error</c> in, tests use string
/// writers. No exit-code logic here.</summary>
internal sealed class ContentLintReporter
{
    public void Render(ContentLintReport report, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (report.DirectoryMissing)
        {
            error.WriteLine("ERROR: data directory does not exist.");
            return;
        }

        var loadedCount = report.ParseErrors.Count == 0
            ? "every"
            : "some";
        output.WriteLine($"content-lint: {loadedCount} known CSV parsed cleanly.");

        foreach (var (path, message) in report.ParseErrors)
        {
            error.WriteLine($"ERROR: {path}");
            error.WriteLine($"  {message}");
        }

        foreach (var unmatched in report.UnmatchedCsvFiles)
        {
            output.WriteLine($"WARN: no matcher registered for '{unmatched}'.");
        }

        if (report.ValidatorErrors.Count > 0)
        {
            error.WriteLine($"ERROR: catalog validator reported {report.ValidatorErrors.Count} issues:");
            foreach (var line in report.ValidatorErrors)
            {
                error.WriteLine($"  - {line}");
            }
        }
    }
}
