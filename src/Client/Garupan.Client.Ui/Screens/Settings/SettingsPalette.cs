using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Settings;

/// <summary>Shared colour + layout constants for the settings screen. Centralised so the
/// layout maths and the renderer agree on every metric.</summary>
internal static class SettingsPalette
{
    public const int TopBarHeight = 56;
    public const int FirstRowY = 132;
    public const int RowHeight = 50;
    public const int SectionGap = 36;
    public const int MarginX = 80;
    public const int ValueAreaWidth = 260;
    public const int ArrowWidth = 34;
    public const int LabelFontSize = 20;

    public static readonly Color Background  = new(12, 14, 20, 255);
    public static readonly Color Panel       = new(20, 24, 32, 255);
    public static readonly Color RowSelected = new(30, 36, 48, 255);
    public static readonly Color Foreground  = new(220, 226, 240, 255);
    public static readonly Color Dim         = new(140, 148, 168, 255);
    public static readonly Color Crimson     = new(196, 36, 56, 255);
    public static readonly Color Arrow       = new(150, 158, 178, 255);
    public static readonly Color ArrowActive = new(232, 236, 246, 255);
    public static readonly Color Restart     = new(214, 168, 92, 255);
    public static readonly Color Section     = new(120, 128, 148, 255);
}
