using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Match;

/// <summary>
/// Shared colours for the Phase-0 top-down match view. Pulled out so renderers don't each
/// carry their own copy of the same constants and a future restyle is a single edit.
/// </summary>
internal static class MatchPalette
{
    public static readonly Color Background    = new(20, 26, 32, 255);
    public static readonly Color FieldBg       = new(36, 48, 56, 255);
    public static readonly Color FieldGrid     = new(46, 60, 70, 255);
    public static readonly Color FieldBorder   = new(180, 90, 100, 255);
    public static readonly Color Foreground    = new(220, 226, 240, 255);
    public static readonly Color Dim           = new(140, 148, 168, 255);
    public static readonly Color Crimson       = new(196, 36, 56, 255);
    public static readonly Color PlayerTeam    = new(70, 200, 130, 255);
    public static readonly Color OpponentTeam  = new(220, 70, 80, 255);
    public static readonly Color KnockedOut    = new(80, 84, 92, 255);
    public static readonly Color Bullet        = new(252, 220, 100, 255);
    public static readonly Color Heading       = new(255, 255, 255, 255);
    public static readonly Color HudPanel      = new(14, 18, 22, 255);
    public static readonly Color HudBarTrack   = new(40, 48, 56, 255);
    public static readonly Color HudChip       = new(28, 34, 42, 255);
    public static readonly Color AmmoLabel     = new(214, 168, 92, 255);
    public static readonly Color ReticleReloading = new(140, 56, 64, 255);
    public static readonly Color Outline       = new(0, 0, 0, 255);
}
