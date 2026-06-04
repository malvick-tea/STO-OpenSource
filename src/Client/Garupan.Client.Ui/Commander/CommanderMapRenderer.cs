using System;
using System.Collections.Generic;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Commander;

/// <summary>
/// Draws the commander's hand-drawn battle map: table-felt background, the cream paper
/// with a faint 100-metre grid, every committed mark (strokes + tokens, in placement
/// order), and the in-progress stroke on top. Pure read — the
/// <see cref="CommanderMapState"/> + <see cref="CommanderMapBounds"/> are passed in each
/// frame; the renderer holds no state.
///
/// Marks are iterated in their <see cref="CommanderMapState.Marks"/> order so a token
/// placed after a stroke layers on top of it — matching the commander's mental model of
/// "what I added later sits on what I added earlier". The active (in-progress) stroke
/// always renders last so the player sees the line they're currently drawing.
/// </summary>
public sealed class CommanderMapRenderer
{
    public void Render(IDrawSurface surface, CommanderMapBounds paper, CommanderMapState state)
    {
        DrawBackground(surface);
        DrawPaper(surface, paper);
        DrawGrid(surface, paper);
        DrawPaperBorder(surface, paper);

        foreach (var mark in state.Marks)
        {
            DrawMark(surface, mark);
        }

        if (state.IsDrawing)
        {
            DrawPolyline(surface, state.ActivePoints, state.ActiveInkColor, state.ActiveThickness);
        }
    }

    private static void DrawBackground(IDrawSurface surface) =>
        surface.Clear(CommanderMapPalette.Background);

    private static void DrawPaper(IDrawSurface surface, CommanderMapBounds paper) =>
        surface.FillRect(paper.X, paper.Y, paper.Width, paper.Height, CommanderMapPalette.Paper);

    private static void DrawPaperBorder(IDrawSurface surface, CommanderMapBounds paper) =>
        surface.StrokeRect(paper.X, paper.Y, paper.Width, paper.Height, 3, CommanderMapPalette.Border);

    /// <summary>Faint 100-metre grid so the commander can gauge distances on the page
    /// without a ruler. Drawn before the marks so ink sits on top of the grid lines.</summary>
    private static void DrawGrid(IDrawSurface surface, CommanderMapBounds paper)
    {
        var step = CommanderMapPalette.PaperGridStepPixels;
        for (var x = paper.X + step; x < paper.Right; x += step)
        {
            surface.DrawLine(x, paper.Y, x, paper.Bottom, 1, CommanderMapPalette.PaperGrid);
        }

        for (var y = paper.Y + step; y < paper.Bottom; y += step)
        {
            surface.DrawLine(paper.X, y, paper.Right, y, 1, CommanderMapPalette.PaperGrid);
        }
    }

    private static void DrawMark(IDrawSurface surface, CommanderMapMark mark)
    {
        switch (mark)
        {
            case CommanderMapStroke stroke:
                DrawPolyline(surface, stroke.Points, stroke.InkColor, stroke.Thickness);
                break;

            case CommanderMapToken token:
                DrawToken(surface, token);
                break;
        }
    }

    private static void DrawPolyline(
        IDrawSurface surface,
        IReadOnlyList<CommanderMapPoint> points,
        Color color,
        int thickness)
    {
        if (points.Count == 0)
        {
            return;
        }

        if (points.Count == 1)
        {
            var dotRadius = Math.Max(1, thickness / 2);
            surface.FillCircle(points[0].X, points[0].Y, dotRadius, color);
            return;
        }

        for (var i = 1; i < points.Count; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            surface.DrawLine(a.X, a.Y, b.X, b.Y, thickness, color);
        }
    }

    private static void DrawToken(IDrawSurface surface, CommanderMapToken token)
    {
        var cx = token.Position.X;
        var cy = token.Position.Y;
        var radius = CommanderToolParameters.TokenRadiusPixels;

        // Cream paper fill so the token reads as a printed counter sitting on the map,
        // not as a hole punched through the page. Outline + label in the token's ink.
        surface.FillCircle(cx, cy, radius, CommanderMapPalette.Paper);
        surface.StrokeCircle(cx, cy, radius, CommanderToolParameters.TokenOutlineThickness, token.InkColor);

        var fontSize = CommanderToolParameters.TokenLabelFontSize;
        var labelWidth = surface.MeasureText(token.Label, fontSize);
        surface.DrawText(token.Label, cx - (labelWidth / 2), cy - (fontSize / 2), fontSize, token.InkColor);
    }
}
