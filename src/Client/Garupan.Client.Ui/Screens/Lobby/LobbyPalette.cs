using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Lobby;

/// <summary>
/// Shared colour + layout constants for the lobby screen. Centralised so the screen
/// orchestrator, the mode list, and per-card rendering stay visually consistent and
/// designers tune one place when the look needs a pass.
/// </summary>
internal static class LobbyPalette
{
    // Top chrome — top bar shared with main menu shell + a title band underneath.
    public const int TopBarHeight = 56;
    public const int TitleY = 96;
    public const int TitleFontSize = 36;
    public const int SubtitleFontSize = 14;

    // Mode-card grid. Cards are laid out horizontally, centred. Two cards in v1.0.
    public const int CardWidth = 360;
    public const int CardHeight = 260;
    public const int CardGap = 32;
    public const int CardPaddingX = 20;
    public const int CardPaddingY = 18;
    public const int CardCornerInset = 4;
    public const int CardBorderThickness = 2;
    public const int CardAccentHeight = 4;
    public const int CardGridY = 168;

    // Card typography.
    public const int CardNameFontSize = 22;
    public const int CardBadgeFontSize = 12;
    public const int CardSummaryFontSize = 14;
    public const int CardSummaryLineHeight = 18;
    public const int CardCtaFontSize = 14;

    // Badge chip metrics.
    public const int BadgeHeight = 22;
    public const int BadgePaddingX = 10;
    public const int BadgeSpacing = 8;

    // CTA strip metrics.
    public const int CtaStripHeight = 32;

    // Footer hint.
    public const int FooterFontSize = 14;
    public const int FooterMarginY = 24;

    // Match the main-menu / settings band so the lobby reads as part of the same shell.
    public static readonly Color Background    = new(12, 14, 20, 255);
    public static readonly Color Panel         = new(20, 24, 32, 255);
    public static readonly Color CardBody      = new(22, 28, 38, 255);
    public static readonly Color CardBorder    = new(40, 48, 64, 255);
    public static readonly Color CardHover     = new(32, 40, 56, 255);
    public static readonly Color Foreground    = new(220, 226, 240, 255);
    public static readonly Color Dim           = new(140, 148, 168, 255);
    public static readonly Color Crimson       = new(196, 36, 56, 255);
    public static readonly Color Badge         = new(30, 36, 48, 255);
    public static readonly Color BadgeText     = new(180, 188, 208, 255);
}
