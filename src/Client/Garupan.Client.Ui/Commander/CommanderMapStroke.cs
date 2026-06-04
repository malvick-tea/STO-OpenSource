using System.Collections.Generic;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Commander;

/// <summary>
/// A finished hand-drawn stroke — an ordered list of vertices plus the ink colour and pen
/// thickness used to lay them down. One stroke is the unit of
/// <see cref="CommanderMapState.Undo"/> for the pen/marker tools — the commander draws a
/// line, presses undo, the line disappears.
/// </summary>
/// <param name="Points">Vertices in stroke order. Always non-empty.</param>
/// <param name="InkColor">RGBA ink colour at the time the stroke was laid down.</param>
/// <param name="Thickness">Pen line thickness in pixels — pencil tool = thin, marker tool = thick.</param>
public sealed record CommanderMapStroke(
    IReadOnlyList<CommanderMapPoint> Points,
    Color InkColor,
    int Thickness) : CommanderMapMark;
