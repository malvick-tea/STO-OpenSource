using System;
using System.IO;

namespace Garupan.Tools.Tests.Fixtures;

/// <summary>RAII helper that owns a fresh <c>%TEMP%/{prefix}-{guid}</c> directory for
/// the lifetime of a test instance and cleans it up on dispose. Cuts the
/// boilerplate that otherwise repeats across every loc-lint / content-lint test class
/// that needs disk fixtures.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    /// <summary>Absolute path of the owned directory.</summary>
    public string Path { get; }

    /// <summary>Convenience: combines <see cref="Path"/> with the given segments.
    /// Equivalent to <see cref="System.IO.Path.Combine(string[])"/> with the root
    /// prepended.</summary>
    public string Combine(params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var parts = new string[segments.Length + 1];
        parts[0] = Path;
        for (var i = 0; i < segments.Length; i++)
        {
            parts[i + 1] = segments[i];
        }

        return System.IO.Path.Combine(parts);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup — Windows may hold a handle briefly after delete.
        }
    }
}
