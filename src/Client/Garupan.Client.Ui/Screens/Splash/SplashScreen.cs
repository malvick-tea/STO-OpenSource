using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Splash;

/// <summary>
/// Boot-time placeholder. Shown immediately after the host opens the window so the user
/// has something to look at while boot stages run. Replaced by main-menu (or error) once
/// boot completes.
/// </summary>
public sealed class SplashScreen : IScreen
{
    private static readonly Color Bg = new(8, 10, 14, 255);
    private static readonly Color Fg = new(220, 226, 240, 255);
    private static readonly Color Crimson = new(196, 36, 56, 255);

    private double _elapsed;

    public string StatusLine { get; set; } = "BOOTING…";

    public void OnEnter()
    {
        _elapsed = 0;
    }

    public void OnExit()
    {
    }

    public void Update(GameTime time, IInputSource input)
    {
        _ = input; // splash ignores user input
        _elapsed += time.TickIntervalSeconds;
    }

    public void Render(IDrawSurface surface)
    {
        surface.Clear(Bg);

        var w = surface.Width;
        var h = surface.Height;

        const string Logo = "STO";
        var logoFont = 96;
        var logoSize = surface.MeasureText(Logo, logoFont);
        surface.DrawText(Logo, (w - logoSize) / 2, h / 2 - 80, logoFont, Fg);

        var stripeY = h / 2 + 24;
        surface.FillRect((w - 240) / 2, stripeY, 240, 4, Crimson);

        var statusFont = 18;
        var statusSize = surface.MeasureText(StatusLine, statusFont);
        surface.DrawText(StatusLine, (w - statusSize) / 2, stripeY + 24, statusFont, Fg);

        // Tiny pulsing dot to make it visually alive.
        var pulse = (System.Math.Sin(_elapsed * 4.0) + 1.0) * 0.5;
        var pulseAlpha = (byte)(120 + (pulse * 135));
        var pulseColor = new Color(Crimson.R, Crimson.G, Crimson.B, pulseAlpha);
        surface.FillRect((w - 8) / 2, stripeY + 60, 8, 8, pulseColor);
    }
}
