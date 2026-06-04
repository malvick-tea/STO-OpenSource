using System.Collections.Generic;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Lobby;

/// <summary>
/// Greedy word-wrap helper for the lobby's summary paragraphs. Splits on whitespace,
/// measures each candidate line through <see cref="IDrawSurface.MeasureText"/>, and
/// emits the longest run that still fits within <c>maxWidth</c>. A word longer than
/// <c>maxWidth</c> still gets its own line — better to overflow than to crop the text.
/// </summary>
/// <remarks>
/// Pulled into its own helper so the same wrapping rule serves the lobby's card body
/// today and any future "long description" card variant without copy-paste.
/// </remarks>
internal static class SummaryLineWrapper
{
    public static IReadOnlyList<string> Wrap(IDrawSurface surface, string text, int maxWidth, int fontSize)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text) || maxWidth <= 0)
        {
            return lines;
        }

        var words = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return lines;
        }

        var current = words[0];
        for (var i = 1; i < words.Length; i++)
        {
            var candidate = current + ' ' + words[i];
            if (surface.MeasureText(candidate, fontSize) <= maxWidth)
            {
                current = candidate;
            }
            else
            {
                lines.Add(current);
                current = words[i];
            }
        }

        lines.Add(current);
        return lines;
    }
}
