using System.Collections.Generic;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Tests.Fixtures;

/// <summary>
/// <see cref="IDrawSurface"/> that records every draw call instead of rasterising. Tests
/// assert on the recorded log to check layout / colour / text content without standing up
/// a real GPU backend. <see cref="MeasureText"/> returns a deterministic monospace
/// approximation so tests can predict text widths.
///
/// Records are immutable value types — order in <see cref="Commands"/> is the call order.
/// </summary>
public sealed class RecordingDrawSurface : IDrawSurface
{
    private const int FixedCharWidth = 7;

    private readonly List<DrawCommand> _commands = new();

    public RecordingDrawSurface(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public IReadOnlyList<DrawCommand> Commands => _commands;

    public void Clear(Color color) => _commands.Add(new DrawClear(color));

    public void FillRect(int x, int y, int w, int h, Color color) =>
        _commands.Add(new DrawFillRect(x, y, w, h, color));

    public void StrokeRect(int x, int y, int w, int h, int thickness, Color color) =>
        _commands.Add(new DrawStrokeRect(x, y, w, h, thickness, color));

    public void DrawLine(int x0, int y0, int x1, int y1, int thickness, Color color) =>
        _commands.Add(new DrawLineCommand(x0, y0, x1, y1, thickness, color));

    public void FillCircle(int cx, int cy, int radius, Color color) =>
        _commands.Add(new DrawFillCircle(cx, cy, radius, color));

    public void StrokeCircle(int cx, int cy, int radius, int thickness, Color color) =>
        _commands.Add(new DrawStrokeCircle(cx, cy, radius, thickness, color));

    public void DrawText(string text, int x, int y, int fontSize, Color color) =>
        _commands.Add(new DrawTextCommand(text, x, y, fontSize, color));

    public int MeasureText(string text, int fontSize) => text.Length * FixedCharWidth * fontSize / 12;
}
