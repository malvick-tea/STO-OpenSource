using System.Text;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Text;

/// <summary>
/// Text layout helpers shared across screens. The Phase-0 implementation is a simple
/// greedy word-wrap built on <see cref="IDrawSurface.MeasureText"/> — good enough for
/// briefings and detail panels. Runtime-grade typography (kerning, glyph hinting,
/// language-aware line-break opportunities) lands with the Engine.Renderer text pass.
///
/// Stays a separate module because both <c>CampaignScreen</c> and
/// <c>MissionBriefingScreen</c> historically carried their own copy of the same routine;
/// future screens will too. One implementation, one bug surface.
/// </summary>
public static class TextLayout
{
    /// <summary>
    /// Draws <paramref name="text"/> word-wrapped inside <paramref name="maxWidth"/> pixels
    /// at <paramref name="fontSize"/>. Returns the vertical extent consumed so the caller
    /// can stack subsequent content beneath the block.
    /// </summary>
    public static int DrawWrapped(
        IDrawSurface surface,
        int x,
        int y,
        int maxWidth,
        int fontSize,
        string text,
        Color color)
    {
        var line = new StringBuilder();
        var cursorY = y;
        var lineHeight = fontSize + 6;

        foreach (var word in text.Split(' '))
        {
            var probe = line.Length == 0 ? word : line + " " + word;
            if (surface.MeasureText(probe, fontSize) > maxWidth && line.Length > 0)
            {
                surface.DrawText(line.ToString(), x, cursorY, fontSize, color);
                cursorY += lineHeight;
                line.Clear();
                line.Append(word);
            }
            else
            {
                if (line.Length > 0)
                {
                    line.Append(' ');
                }

                line.Append(word);
            }
        }

        if (line.Length > 0)
        {
            surface.DrawText(line.ToString(), x, cursorY, fontSize, color);
            cursorY += lineHeight;
        }

        return cursorY - y;
    }
}
