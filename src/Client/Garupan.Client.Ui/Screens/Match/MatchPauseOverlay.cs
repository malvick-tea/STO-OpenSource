using Garupan.Client.Core.Services;
using Garupan.Localisation;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Match;

/// <summary>What the player chose on the pause menu.</summary>
public enum PauseAction
{
    None,
    Resume,
    Abandon,
}

/// <summary>
/// The in-match pause menu: a darkening scrim, a centred panel, and Resume / Abandon
/// buttons. Owns its own layout + hit-testing so <see cref="MatchScreen"/> only has to
/// track the paused flag and route clicks. Drawing it is the screen's signal that the
/// simulation is frozen — the overlay itself ticks nothing.
/// </summary>
public sealed class MatchPauseOverlay
{
    private const int PanelWidth = 380;
    private const int PanelHeight = 244;
    private const int ButtonWidth = 300;
    private const int ButtonHeight = 46;
    private const int FirstButtonOffset = 96;
    private const int ButtonGap = 14;

    private static readonly Color Scrim       = new(6, 8, 12, 205);
    private static readonly Color Panel       = new(20, 24, 32, 255);
    private static readonly Color Foreground  = new(220, 226, 240, 255);
    private static readonly Color Dim         = new(140, 148, 168, 255);
    private static readonly Color Crimson     = new(196, 36, 56, 255);
    private static readonly Color Button      = new(30, 36, 48, 255);
    private static readonly Color ButtonHover = new(46, 54, 70, 255);

    private readonly LocalizationService _l10n;

    public MatchPauseOverlay(LocalizationService l10n) => _l10n = l10n;

    /// <summary>Which button (if any) the cursor is over. Pure geometry — static so the
    /// hit-test is exercised without standing up a localisation catalogue.</summary>
    public static PauseAction ActionAt(int mouseX, int mouseY, int surfaceWidth, int surfaceHeight)
    {
        if (Contains(ResumeRect(surfaceWidth, surfaceHeight), mouseX, mouseY))
        {
            return PauseAction.Resume;
        }

        if (Contains(AbandonRect(surfaceWidth, surfaceHeight), mouseX, mouseY))
        {
            return PauseAction.Abandon;
        }

        return PauseAction.None;
    }

    public void Render(IDrawSurface surface, PauseAction hovered)
    {
        var w = surface.Width;
        var h = surface.Height;
        surface.FillRect(0, 0, w, h, Scrim);

        var panelX = (w - PanelWidth) / 2;
        var panelY = (h - PanelHeight) / 2;
        surface.FillRect(panelX, panelY, PanelWidth, PanelHeight, Panel);
        surface.FillRect(panelX, panelY, PanelWidth, 4, Crimson);

        var title = _l10n.T(L10nKeys.Match.Paused);
        surface.DrawText(title, (w - surface.MeasureText(title, 40)) / 2, panelY + 26, 40, Foreground);

        DrawButton(surface, ResumeRect(w, h), _l10n.T(L10nKeys.Match.Resume), hovered == PauseAction.Resume);
        DrawButton(surface, AbandonRect(w, h), _l10n.T(L10nKeys.Match.Abandon), hovered == PauseAction.Abandon);

        var hint = _l10n.T(L10nKeys.Match.PauseHint);
        surface.DrawText(hint, (w - surface.MeasureText(hint, 13)) / 2, panelY + PanelHeight - 32, 13, Dim);
    }

    private static (int X, int Y, int W, int H) ResumeRect(int surfaceWidth, int surfaceHeight)
    {
        var x = (surfaceWidth - ButtonWidth) / 2;
        var y = ((surfaceHeight - PanelHeight) / 2) + FirstButtonOffset;
        return (x, y, ButtonWidth, ButtonHeight);
    }

    private static (int X, int Y, int W, int H) AbandonRect(int surfaceWidth, int surfaceHeight)
    {
        var resume = ResumeRect(surfaceWidth, surfaceHeight);
        return (resume.X, resume.Y + ButtonHeight + ButtonGap, ButtonWidth, ButtonHeight);
    }

    private static bool Contains((int X, int Y, int W, int H) rect, int pointX, int pointY) =>
        pointX >= rect.X && pointX < rect.X + rect.W &&
        pointY >= rect.Y && pointY < rect.Y + rect.H;

    private void DrawButton(IDrawSurface surface, (int X, int Y, int W, int H) rect, string label, bool hovered)
    {
        surface.FillRect(rect.X, rect.Y, rect.W, rect.H, hovered ? ButtonHover : Button);
        if (hovered)
        {
            surface.FillRect(rect.X, rect.Y, 4, rect.H, Crimson);
        }

        var labelColor = hovered ? Foreground : Dim;
        var labelX = rect.X + ((rect.W - surface.MeasureText(label, 20)) / 2);
        surface.DrawText(label, labelX, rect.Y + 12, 20, labelColor);
    }
}
