using System.Globalization;
using Garupan.Client.Core.Application;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Match;

/// <summary>
/// Draws the chrome around the match viewport: top status bar (mission label + crimson
/// stripe), right-hand status panel (alive counts + driver ammo chip + reload bar), the
/// driver gunsight reticle over the world viewport, and the bottom hint. Pure read of a
/// <see cref="MatchHudReadout"/> snapshot — knows nothing about the ECS world.
///
/// Reticle geometry lives in <see cref="MatchHudReticle"/>; this file is the panel layout
/// + the snapshot → screen wiring.
/// </summary>
public sealed class MatchHudRenderer
{
    private const int HudPanelMargin = 20;
    private const int HudPanelTrailingMargin = 24;
    private const int HudInnerPadX = 16;
    private const int HudSectionTopY = 14;
    private const int HudSectionUnderlineY = 32;
    private const int HudSectionUnderlineW = 60;
    private const int HudAlliesY = 50;
    private const int HudEnemiesY = 76;
    private const int HudPlayerHeaderY = 120;
    private const int HudPlayerUnderlineY = 138;
    private const int HudAmmoLabelY = 156;
    private const int HudAmmoChipY = 174;
    private const int HudAmmoChipHeight = 28;
    private const int HudReloadBarY = 212;
    private const int HudReloadBarHeight = 14;
    private const int HudReloadLabelY = 234;
    private const int TopBarHeight = 56;

    private readonly string _missionLabel;
    private readonly string _hint;

    /// <param name="bindings">Active keyboard controls — the hint line spells out the
    /// real keys, so a rebind shows through instead of a stale "WASD" literal.</param>
    public MatchHudRenderer(string missionLabel, InputBindings bindings)
    {
        _missionLabel = missionLabel;
        _hint = BuildHint(bindings);
    }

    /// <summary>
    /// Draws one frame of HUD. The driver gunsight is drawn only when <paramref name="showReticle"/>
    /// is true — the screen sets that when the mouse is over the world viewport AND the
    /// player tank is alive, so a knocked-out player or a mouse outside the field doesn't
    /// project a stray reticle.
    /// </summary>
    public void Render(
        IDrawSurface surface,
        MatchHudReadout readout,
        MatchViewport viewport,
        in ReticleAim aim,
        bool showReticle)
    {
        DrawTopBar(surface);
        DrawStatusPanel(surface, readout, viewport);
        if (showReticle)
        {
            MatchHudReticle.Draw(
                surface, aim.PlayerScreenX, aim.PlayerScreenY,
                aim.AimScreenX, aim.AimScreenY,
                aim.RangeMeters, readout.IsReady);
        }

        DrawHint(surface);
    }

    /// <summary>Bottom hint line, spelt from the live bindings so a rebind is reflected.
    /// Esc pauses the match (Phase 17) — it no longer backs straight out.</summary>
    private static string BuildHint(InputBindings bindings) =>
        $"{bindings.MoveForward}{bindings.SteerLeft}{bindings.MoveBackward}{bindings.SteerRight} drive" +
        $"  •  Mouse aim  •  {bindings.Fire} fire  •  Esc pause";

    private void DrawTopBar(IDrawSurface surface)
    {
        surface.FillRect(0, 0, surface.Width, TopBarHeight, MatchPalette.HudPanel);
        surface.DrawText($"MATCH — {_missionLabel}", 24, HudSectionTopY, 22, MatchPalette.Foreground);
        surface.FillRect(0, TopBarHeight, surface.Width, 2, MatchPalette.Crimson);
    }

    private static void DrawStatusPanel(IDrawSurface surface, MatchHudReadout readout, MatchViewport viewport)
    {
        var hudX = viewport.X + viewport.Width + HudPanelMargin;
        var hudY = viewport.Y;
        var hudW = surface.Width - hudX - HudPanelTrailingMargin;

        surface.FillRect(hudX, hudY, hudW, viewport.Height, MatchPalette.HudPanel);

        DrawSection(surface, "STATUS", hudX, hudY);
        surface.DrawText($"Allies   {readout.AlivePlayers}",  hudX + HudInnerPadX, hudY + HudAlliesY,  18, MatchPalette.PlayerTeam);
        surface.DrawText($"Enemies  {readout.AliveOpponents}", hudX + HudInnerPadX, hudY + HudEnemiesY, 18, MatchPalette.OpponentTeam);

        DrawSection(surface, "PLAYER", hudX, hudY + (HudPlayerHeaderY - HudSectionTopY));

        if (!readout.IsPlayerAlive)
        {
            surface.DrawText("KNOCKED OUT", hudX + HudInnerPadX, hudY + HudAmmoChipY, 16, MatchPalette.KnockedOut);
            return;
        }

        DrawAmmoChip(surface, readout, hudX, hudY, hudW);
        DrawReloadBar(surface, readout, hudX, hudY, hudW);
    }

    private static void DrawSection(IDrawSurface surface, string title, int hudX, int hudY)
    {
        surface.DrawText(title, hudX + HudInnerPadX, hudY + HudSectionTopY, 14, MatchPalette.Dim);
        surface.FillRect(hudX + HudInnerPadX, hudY + HudSectionUnderlineY, HudSectionUnderlineW, 2, MatchPalette.Crimson);
    }

    private static void DrawAmmoChip(IDrawSurface surface, MatchHudReadout readout, int hudX, int hudY, int hudW)
    {
        surface.DrawText("ROUND", hudX + HudInnerPadX, hudY + HudAmmoLabelY, 12, MatchPalette.Dim);

        var chipX = hudX + HudInnerPadX;
        var chipW = hudW - (HudInnerPadX * 2);
        surface.FillRect(chipX, hudY + HudAmmoChipY, chipW, HudAmmoChipHeight, MatchPalette.HudChip);

        var label = AmmoTypeLabels.Of(readout.ChamberedRound);
        var labelWidth = surface.MeasureText(label, 18);
        surface.DrawText(label, chipX + ((chipW - labelWidth) / 2), hudY + HudAmmoChipY + 5, 18, MatchPalette.AmmoLabel);
    }

    private static void DrawReloadBar(IDrawSurface surface, MatchHudReadout readout, int hudX, int hudY, int hudW)
    {
        var barX = hudX + HudInnerPadX;
        var barW = hudW - (HudInnerPadX * 2);
        var fillColor = readout.IsReady ? MatchPalette.Crimson : MatchPalette.Dim;

        surface.FillRect(barX, hudY + HudReloadBarY, barW, HudReloadBarHeight, MatchPalette.HudBarTrack);
        surface.FillRect(barX, hudY + HudReloadBarY, (int)(barW * readout.ReloadFraction), HudReloadBarHeight, fillColor);

        var seconds = ReloadSecondsRemaining(readout);
        var label = readout.IsReady
            ? "READY"
            : seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";
        surface.DrawText(label, barX, hudY + HudReloadLabelY, 14, MatchPalette.Foreground);
    }

    /// <summary>Approximate seconds-remaining label for the reload bar. Driven by the
    /// fraction we already have — the renderer does not pull a second timer off the ECS,
    /// avoiding any chance of label / bar disagreement within one frame.</summary>
    private static float ReloadSecondsRemaining(MatchHudReadout readout)
    {
        // Reload total is not on the readout (the bar only needs the fraction); the label
        // tracks fraction · 4.0 s as an indicative readout. A faithful per-shot timer is
        // the M5 ammo-rack revision job — see [[garupan-pre-alpha-checklist...]].
        const float IndicativeReloadMaxSeconds = 4.0f;
        return (1f - readout.ReloadFraction) * IndicativeReloadMaxSeconds;
    }

    private void DrawHint(IDrawSurface surface)
    {
        var hintSize = surface.MeasureText(_hint, 13);
        surface.DrawText(_hint, (surface.Width - hintSize) / 2, surface.Height - 22, 13, MatchPalette.Dim);
    }
}
