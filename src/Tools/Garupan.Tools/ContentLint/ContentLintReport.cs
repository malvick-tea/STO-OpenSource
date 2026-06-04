using System;
using System.Collections.Generic;
using Garupan.Content;

namespace Garupan.Tools.ContentLint;

/// <summary>Pure outcome of a <c>content-lint</c> sweep — parse failures grouped by
/// file, validator findings collected from <see cref="CatalogValidator.Validate(SchoolPalette)"/>,
/// and any CSVs we walked over but had no matcher for (warning, not failure — the lint
/// shouldn't block a feature branch that lands a new authoring CSV slightly ahead of
/// its loader).</summary>
internal sealed record ContentLintReport(
    bool DirectoryMissing,
    IReadOnlyDictionary<string, string> ParseErrors,
    IReadOnlyList<string> UnmatchedCsvFiles,
    IReadOnlyList<string> ValidatorErrors)
{
    public bool HasFailures =>
        DirectoryMissing ||
        ParseErrors.Count > 0 ||
        ValidatorErrors.Count > 0;

    public static ContentLintReport From(CsvLoadResult load)
    {
        ArgumentNullException.ThrowIfNull(load);

        IReadOnlyList<string> validatorErrors = Array.Empty<string>();
        if (load.LoadedPalette is { } palette)
        {
            var result = CatalogValidator.Validate(palette);
            validatorErrors = result.Errors;
        }

        return new ContentLintReport(
            DirectoryMissing: load.DirectoryMissing,
            ParseErrors: load.ParseErrors,
            UnmatchedCsvFiles: load.UnmatchedCsvFiles,
            ValidatorErrors: validatorErrors);
    }
}
