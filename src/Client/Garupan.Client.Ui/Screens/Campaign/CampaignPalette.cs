using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Campaign;

/// <summary>Shared colour and metric constants for the campaign-graph screen.
/// Pulled out so renderers don't each carry their own copy.</summary>
internal static class CampaignPalette
{
    public const int NodeRadius = 26;
    public const int LayoutMarginTop = 110;
    public const int LayoutMarginBottom = 220;
    public const int LayoutMarginX = 60;

    public static readonly Color Background  = new(8, 10, 14, 255);
    public static readonly Color Panel       = new(20, 24, 32, 255);
    public static readonly Color PanelHover  = new(30, 36, 48, 255);
    public static readonly Color PanelActive = new(46, 26, 30, 255);
    public static readonly Color Foreground  = new(220, 226, 240, 255);
    public static readonly Color Dim         = new(140, 148, 168, 255);
    public static readonly Color Crimson     = new(196, 36, 56, 255);
    public static readonly Color Edge        = new(60, 68, 84, 255);
    public static readonly Color EdgeBright  = new(180, 90, 100, 255);

    // Progression status accents.
    public static readonly Color LockedFill  = new(14, 16, 22, 255);
    public static readonly Color LockedText  = new(86, 92, 108, 255);
    public static readonly Color Complete    = new(120, 200, 140, 255);
    public static readonly Color CompleteEdge = new(70, 120, 86, 255);
}
