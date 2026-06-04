using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.MainMenu;

/// <summary>Shared colour + layout constants for the main-menu screen.
/// Centralised so nav-list + ComingSoon stub render with the same look.</summary>
internal static class MainMenuPalette
{
    public const int ListX = 32;
    public const int ListY = 120;
    public const int RowHeight = 44;
    public const int RowWidth = 280;
    public const int LabelFontSize = 22;

    public static readonly Color Background  = new(12, 14, 20, 255);
    public static readonly Color Panel       = new(20, 24, 32, 255);
    public static readonly Color Hover       = new(30, 36, 48, 255);
    public static readonly Color Foreground  = new(220, 226, 240, 255);
    public static readonly Color Dim         = new(140, 148, 168, 255);
    public static readonly Color Crimson     = new(196, 36, 56, 255);
}
