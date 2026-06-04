using Garupan.Client.Core.Services;
using Garupan.Localisation;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Campaign.Briefing;

/// <summary>
/// Bottom-of-left-column DEPLOY button. Owns its rect computation, hit-test, and the
/// hover-coloured render. The screen passes the hovered flag in each frame; the button
/// has no internal "active" state of its own.
/// </summary>
public sealed class DeployButton
{
    private const int ButtonHeight = 56;
    private const int LeftMargin = 64;
    private const int BottomInsetFromHint = 80 + 24;
    private const int MidColumnPadding = 110;

    private readonly LocalizationService _l10n;

    public DeployButton(LocalizationService l10n)
    {
        _l10n = l10n;
    }

    public (int X, int Y, int W, int H) Rect(int surfaceWidth, int surfaceHeight)
    {
        var midX = surfaceWidth / 2;
        var width = midX - MidColumnPadding;
        var x = LeftMargin;
        var y = surfaceHeight - BottomInsetFromHint - ButtonHeight;
        return (x, y, width, ButtonHeight);
    }

    public bool HitTest(int mouseX, int mouseY, int surfaceWidth, int surfaceHeight)
    {
        if (surfaceWidth == 0)
        {
            return false;
        }

        var (bx, by, bw, bh) = Rect(surfaceWidth, surfaceHeight);
        return mouseX >= bx && mouseX < bx + bw && mouseY >= by && mouseY < by + bh;
    }

    public void Render(IDrawSurface surface, bool hovered)
    {
        var (bx, by, bw, bh) = Rect(surface.Width, surface.Height);
        var fill = hovered ? BriefingPalette.DeployHover : BriefingPalette.DeployIdle;

        surface.FillRect(bx, by, bw, bh, fill);
        surface.StrokeRect(bx, by, bw, bh, 2, BriefingPalette.Foreground);

        var label = _l10n.T(L10nKeys.Campaign.BriefingDeploy);
        var labelWidth = surface.MeasureText(label, 20);
        surface.DrawText(label, bx + (bw - labelWidth) / 2, by + (bh - 20) / 2, 20, BriefingPalette.Foreground);
    }
}
