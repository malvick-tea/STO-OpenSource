using System;
using System.IO;

namespace Garupan.Content.Validation;

internal static class ContentResourcePath
{
    private const string ResourceScheme = "res://";
    private const int MaximumPathLength = 1024;
    private const int MaximumSegmentLength = 255;
    private static readonly char[] PortableInvalidCharacters = ['<', '>', '"', '|', '?', '*'];

    public static string Require(string value, int rowIndex, string columnName)
    {
        var path = value.Trim();
        if (path.Length == 0)
        {
            throw Invalid(rowIndex, columnName, "is empty");
        }

        if (path.Length > MaximumPathLength)
        {
            throw Invalid(rowIndex, columnName, "exceeds the maximum resource path length");
        }

        if (!path.StartsWith(ResourceScheme, StringComparison.Ordinal))
        {
            throw Invalid(rowIndex, columnName, "must use the res:// scheme");
        }

        var relative = path[ResourceScheme.Length..];
        if (relative.Length == 0
            || relative.Contains('\0')
            || relative.Contains('\\', StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            throw Invalid(rowIndex, columnName, "contains an invalid resource path");
        }

        foreach (var segment in relative.Split('/'))
        {
            if (segment.Length == 0
                || segment.Length > MaximumSegmentLength
                || segment is "." or ".."
                || segment.EndsWith(' ')
                || segment.EndsWith('.')
                || segment.Contains(':', StringComparison.Ordinal)
                || segment.IndexOfAny(PortableInvalidCharacters) >= 0
                || ContainsControlCharacter(segment)
                || IsWindowsDeviceName(segment))
            {
                throw Invalid(rowIndex, columnName, "contains an unsafe path segment");
            }
        }

        return ResourceScheme + string.Join('/', relative.Split('/'));
    }

    private static bool ContainsControlCharacter(string segment)
    {
        foreach (var character in segment)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWindowsDeviceName(string segment)
    {
        var stem = segment.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return stem.Length == 4
            && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
            && stem[3] is >= '1' and <= '9';
    }

    private static InvalidDataException Invalid(
        int rowIndex,
        string columnName,
        string reason) =>
        new($"Content CSV row {rowIndex + 1}: column \"{columnName}\" {reason}.");
}
