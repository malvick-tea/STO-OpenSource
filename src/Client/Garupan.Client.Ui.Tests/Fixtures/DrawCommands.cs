using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Tests.Fixtures;

/// <summary>Base type for every draw call captured by <see cref="RecordingDrawSurface"/>.
/// Records so equality + deconstruction work in test assertions.</summary>
public abstract record DrawCommand;

public sealed record DrawClear(Color Color) : DrawCommand;

public sealed record DrawFillRect(int X, int Y, int W, int H, Color Color) : DrawCommand;

public sealed record DrawStrokeRect(int X, int Y, int W, int H, int Thickness, Color Color) : DrawCommand;

public sealed record DrawLineCommand(int X0, int Y0, int X1, int Y1, int Thickness, Color Color) : DrawCommand;

public sealed record DrawFillCircle(int Cx, int Cy, int Radius, Color Color) : DrawCommand;

public sealed record DrawStrokeCircle(int Cx, int Cy, int Radius, int Thickness, Color Color) : DrawCommand;

public sealed record DrawTextCommand(string Text, int X, int Y, int FontSize, Color Color) : DrawCommand;
