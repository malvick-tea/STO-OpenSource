using System;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Garage;

/// <summary>
/// Right-column stat block in the garage. Phase-0 reads hard-coded the medium tank figures —
/// when the catalogue-driven UI lands the rows come from a <c>TankSpec</c> + the
/// chambered <c>AmmoSpec</c>, but the renderer's signature already takes (label, value)
/// rows so the data swap is a one-call-site change.
/// </summary>
public sealed class TankStatBlockRenderer
{
    private const int PanelWidth = 320;
    private const int PanelHeight = 240;
    private const int PanelMarginRight = 360;
    private const int PanelMarginTop = 80;
    private const int RowStride = 30;

    private static readonly (string Label, string Value)[] Stats =
    {
        ("ARMOR",  "80 mm front"),
        ("GUN",    "7.5 cm medium gun"),
        ("PEN",    "132 mm @ 100 m"),
        ("MAX SPD", "40 km/h"),
        ("HP/TON", "11.7"),
    };

    public void Render(IDrawSurface surface)
    {
        var x = surface.Width - PanelMarginRight;
        var y = PanelMarginTop;

        surface.FillRect(x, y, PanelWidth, PanelHeight, GaragePalette.Panel);
        surface.DrawText("the medium tank", x + 16, y + 16, 22, GaragePalette.Foreground);
        surface.FillRect(x + 16, y + 46, 60, 2, GaragePalette.Crimson);

        var rowY = y + 64;
        for (var i = 0; i < Stats.Length; i++)
        {
            DrawRow(surface, x + 16, rowY + (i * RowStride), Stats[i].Label, Stats[i].Value);
        }
    }

    private static void DrawRow(IDrawSurface surface, int x, int y, string label, string value)
    {
        surface.DrawText(label, x, y, 14, GaragePalette.Dim);
        var labelWidth = surface.MeasureText(label, 14);
        surface.DrawText(value, x + Math.Max(96, labelWidth + 16), y, 16, GaragePalette.Foreground);
    }
}
