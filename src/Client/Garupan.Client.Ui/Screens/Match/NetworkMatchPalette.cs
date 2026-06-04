using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Match;

/// <summary>
/// Constants for <see cref="NetworkMatchScreen"/>'s minimal first-cut renderer. The
/// network match screen is intentionally crude (dot-render of snapshot entities + a
/// status line) — Pillar-2 runtime visuals will graduate the network path onto the
/// same `MatchWorldRenderer` / `MatchHudRenderer` the local screen uses in a later
/// phase. Right now it has its own palette so its temporary look is self-contained.
/// </summary>
internal static class NetworkMatchPalette
{
    public const int TopBarHeight = 36;
    public const int StatusFontSize = 18;
    public const int HintFontSize = 14;

    /// <summary>Height of the mode-mismatch notice band drawn directly below the top
    /// bar when the joined server hosts a different mode than the lobby card picked.</summary>
    public const int MismatchBandHeight = 22;

    /// <summary>Baseline offset of the mismatch text within its band.</summary>
    public const int MismatchBandTextOffsetY = 4;
    public const int TankRadiusPixels = 8;
    public const int ProjectileRadiusPixels = 3;
    public const int VerdictFontSize = 48;
    public const float DefaultHalfExtentMeters = 80f;

    public static readonly Color Background = new(12, 14, 20, 255);
    public static readonly Color Panel = new(20, 24, 32, 255);
    public static readonly Color GridLine = new(28, 32, 42, 255);

    /// <summary>Crimson fill of the pre-play loading bar — the brand accent, reused so the loading
    /// screen reads as part of the same shell.</summary>
    public static readonly Color LoadingBar = new(196, 36, 56, 255);
    public static readonly Color Foreground = new(220, 226, 240, 255);
    public static readonly Color Dim = new(140, 148, 168, 255);
    public static readonly Color Warn = new(228, 124, 88, 255);
    public static readonly Color SelfTank = new(232, 196, 60, 255);
    public static readonly Color OtherTank = new(196, 36, 56, 255);
    public static readonly Color Projectile = new(200, 200, 208, 255);
    public static readonly Color KnockedOut = new(80, 90, 108, 255);

    /// <summary>Translucent scrim drawn over the field behind the match-over verdict so
    /// the banner reads clearly above the dot-render beneath it.</summary>
    public static readonly Color VerdictScrim = new(8, 10, 14, 212);

    /// <summary>VICTORY banner ink — a confident green that reads as a win.</summary>
    public static readonly Color VerdictVictory = new(96, 200, 120, 255);

    /// <summary>DEFEAT banner ink — a clear red, distinct from the live enemy-tank red.</summary>
    public static readonly Color VerdictDefeat = new(220, 72, 72, 255);

    /// <summary>DRAW banner ink — neutral slate; nobody won.</summary>
    public static readonly Color VerdictDraw = new(184, 192, 208, 255);
}
