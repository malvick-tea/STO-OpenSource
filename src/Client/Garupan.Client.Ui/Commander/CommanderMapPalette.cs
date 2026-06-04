using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Commander;

/// <summary>
/// Colours for the commander's paper map. Centralised so the layout, the renderer, and
/// the screen all read the same hex values — a future restyle (school-themed paper, for
/// instance) is one edit instead of a search-and-replace.
///
/// The paper is a desaturated cream; the primary ink is a black-brown that reads as
/// pencil / fountain-pen on the cream rather than as harsh black. Accent ink is a muted
/// crimson for tagging objectives without the page looking like a comic-book key.
/// </summary>
internal static class CommanderMapPalette
{
    public const int PaperGridStepPixels = 64;
    public const int PaperMargin = 24;
    public const int TopBarHeight = 56;
    public const int ToolbarHeight = 64;
    public const int DefaultStrokeThickness = 3;

    public static readonly Color Background = new(38, 32, 26, 255);    // table felt behind the paper
    public static readonly Color Paper      = new(232, 220, 188, 255); // aged cream paper
    public static readonly Color PaperGrid  = new(196, 178, 138, 90);  // very faint 100m grid
    public static readonly Color Border     = new(120, 96, 56, 255);   // map binding
    public static readonly Color InkPrimary = new(40, 30, 24, 255);    // primary pen — black-brown
    public static readonly Color InkAccent  = new(160, 30, 30, 255);   // accent pen — muted crimson
    public static readonly Color HudPanel   = new(14, 18, 22, 255);    // matches MatchPalette.HudPanel
    public static readonly Color Foreground = new(220, 226, 240, 255); // matches MatchPalette.Foreground
    public static readonly Color Dim        = new(140, 148, 168, 255); // matches MatchPalette.Dim
    public static readonly Color SelectionRing = new(232, 168, 92, 255); // toolbar tool-selected halo
}
