using System;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Match;

/// <summary>
/// Draws the match's pre-play loading screen: a title, a horizontal bar that fills with the
/// scene-asset preload, the percentage + current stage label, and a connection sub-status line.
/// Pure presentation over <see cref="IDrawSurface"/> (like <see cref="NetworkMatchSkyBackdrop"/>) so
/// it stays headless-testable — <see cref="NetworkMatchScreen"/> owns the state and passes it in.
/// </summary>
internal static class MatchLoadingView
{
    private const int BarWidth = 420;
    private const int BarHeight = 14;
    private const int TitleFontSize = 30;
    private const int LabelFontSize = 16;
    private const int TitleGapY = 64;
    private const int LabelGapY = 10;
    private const int SubStatusGapY = 34;
    private const string Title = "PREPARING BATTLE";

    public static void Draw(IDrawSurface surface, float progress, string stageLabel, string subStatus)
    {
        var clamped = Math.Clamp(progress, 0f, 1f);
        var centerX = surface.Width / 2;
        var barX = centerX - (BarWidth / 2);
        var barY = surface.Height / 2;

        var titleWidth = surface.MeasureText(Title, TitleFontSize);
        surface.DrawText(Title, centerX - (titleWidth / 2), barY - TitleGapY, TitleFontSize, NetworkMatchPalette.Foreground);

        // Track, then the crimson fill clamped to the completed fraction, then a hairline border.
        surface.FillRect(barX, barY, BarWidth, BarHeight, NetworkMatchPalette.Panel);
        var fill = (int)(BarWidth * clamped);
        if (fill > 0)
        {
            surface.FillRect(barX, barY, fill, BarHeight, NetworkMatchPalette.LoadingBar);
        }

        surface.StrokeRect(barX, barY, BarWidth, BarHeight, 1, NetworkMatchPalette.GridLine);

        var readout = $"{(int)(clamped * 100f)}%";
        if (stageLabel.Length > 0)
        {
            readout = $"{readout}  ·  {stageLabel}";
        }

        var readoutWidth = surface.MeasureText(readout, LabelFontSize);
        surface.DrawText(readout, centerX - (readoutWidth / 2), barY + BarHeight + LabelGapY, LabelFontSize, NetworkMatchPalette.Dim);

        if (subStatus.Length > 0)
        {
            var subWidth = surface.MeasureText(subStatus, LabelFontSize);
            surface.DrawText(subStatus, centerX - (subWidth / 2), barY + BarHeight + SubStatusGapY, LabelFontSize, NetworkMatchPalette.Dim);
        }
    }
}
