using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Match;

/// <summary>
/// Pure drawing of the driver gunsight in the Phase-0 top-down view: a four-arm reticle
/// at the aim point (the mouse cursor inside the world viewport), a faint aim-line from
/// the player's tank to that aim point, and a range readout in metres. State-coloured —
/// bright crimson when the gun is ready, dim red while reloading.
///
/// All-static: every call takes the points + state it needs and writes to the surface, so
/// the reticle has no own state to drift out of sync with the renderer between frames.
/// </summary>
public static class MatchHudReticle
{
    private const int ReticleArmLength = 14;
    private const int ReticleArmGap = 6;
    private const int ReticleThickness = 2;
    private const int CentreDotRadius = 2;
    private const int RangeFontSize = 12;
    private const int RangeOffsetY = 18;

    /// <summary>
    /// Draws the aim line + reticle + range label. <paramref name="rangeMeters"/> is the
    /// real-world distance from the player to the aim point — the renderer measures it
    /// once (it has the viewport, the player position, and the mouse) and passes the
    /// scalar in so this routine stays geometry-only.
    /// </summary>
    public static void Draw(
        IDrawSurface surface,
        int playerScreenX, int playerScreenY,
        int aimScreenX, int aimScreenY,
        float rangeMeters,
        bool isReady)
    {
        var color = ColorFor(isReady);

        DrawAimLine(surface, playerScreenX, playerScreenY, aimScreenX, aimScreenY, color);
        DrawReticle(surface, aimScreenX, aimScreenY, color);
        DrawRangeLabel(surface, aimScreenX, aimScreenY, rangeMeters, color);
    }

    /// <summary>Crimson when the gun is ready, the dim variant while reloading — gives
    /// the player a peripheral cue without animating the reticle.</summary>
    public static Color ColorFor(bool isReady) => isReady ? MatchPalette.Crimson : MatchPalette.ReticleReloading;

    private static void DrawAimLine(IDrawSurface s, int x0, int y0, int x1, int y1, Color color) =>
        s.DrawLine(x0, y0, x1, y1, 1, color);

    private static void DrawReticle(IDrawSurface s, int cx, int cy, Color color)
    {
        // Four arms with a centred gap so the player can see what the reticle is on.
        s.DrawLine(cx - ReticleArmLength - ReticleArmGap, cy, cx - ReticleArmGap, cy, ReticleThickness, color);
        s.DrawLine(cx + ReticleArmGap, cy, cx + ReticleArmLength + ReticleArmGap, cy, ReticleThickness, color);
        s.DrawLine(cx, cy - ReticleArmLength - ReticleArmGap, cx, cy - ReticleArmGap, ReticleThickness, color);
        s.DrawLine(cx, cy + ReticleArmGap, cx, cy + ReticleArmLength + ReticleArmGap, ReticleThickness, color);
        s.FillCircle(cx, cy, CentreDotRadius, color);
    }

    private static void DrawRangeLabel(IDrawSurface s, int cx, int cy, float rangeMeters, Color color)
    {
        var label = $"{(int)System.MathF.Round(rangeMeters)} m";
        var labelWidth = s.MeasureText(label, RangeFontSize);
        s.DrawText(label, cx - (labelWidth / 2), cy + RangeOffsetY, RangeFontSize, color);
    }
}
