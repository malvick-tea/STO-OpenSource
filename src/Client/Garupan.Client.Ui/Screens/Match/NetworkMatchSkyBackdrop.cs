using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Match;

/// <summary>
/// Draws the match scene's atmospheric backdrop on the 2D surface BEFORE the 3D tank
/// composite: a banded sky gradient (zenith → horizon haze) over a banded ground gradient
/// (horizon haze → near-camera earth), split by a horizon line. The 3D scene composites on
/// top with a transparent sky, so this backdrop is what fills the field behind the tanks —
/// turning the otherwise-empty void into a readable battlefield.
/// </summary>
/// <remarks>
/// The bands are featureless flat colours, so the fixed 2D horizon reads correctly under
/// the rotating chase camera (a plain gradient looks the same from any heading). A true
/// perspective 3D ground plane — tanks grounded on a textured floor — is a later step on
/// the engine's <c>FloorMesh</c> / <c>FloorPrimitiveUploader</c>; this backdrop is the
/// cheap, correct first read that needs no backend change and leaves the garage untouched.
/// </remarks>
internal static class NetworkMatchSkyBackdrop
{
    /// <summary>Horizon position as a fraction of surface height from the top. ~0.3 keeps
    /// the horizon high so most of the frame is ground — matching the chase camera's
    /// downward look. Visual-calibration constant (tune with the camera).</summary>
    internal const float HorizonFraction = 0.3f;

    // Fine procedural bands avoid visible stepping and do not introduce a sampled sky
    // texture that can shimmer while the chase camera moves.
    internal const int SkyBandCount = 96;
    internal const int GroundBandCount = 64;

    /// <summary>Sky zenith. Mirrors the engine's <c>SkySetup</c> zenith so the 2D backdrop
    /// and the 3D scene's own (transparent) sky agree on the palette.</summary>
    internal static readonly Color Zenith = Color.FromRgb(96, 124, 166);
    internal static readonly Color Horizon = Color.FromRgb(192, 198, 204);
    internal static readonly Color GroundNear = Color.FromRgb(118, 122, 96);
    internal static readonly Color GroundFar = Color.FromRgb(70, 74, 58);

    private static readonly Color HorizonLine = Color.FromRgb(150, 152, 150);

    public static void Draw(IDrawSurface surface)
    {
        var width = surface.Width;
        var height = surface.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var horizonY = (int)(height * HorizonFraction);
        DrawBands(surface, width, top: 0, bottom: horizonY, SkyBandCount, Zenith, Horizon);
        DrawBands(surface, width, top: horizonY, bottom: height, GroundBandCount, GroundNear, GroundFar);
        surface.DrawLine(0, horizonY, width, horizonY, 2, HorizonLine);
    }

    private static void DrawBands(IDrawSurface surface, int width, int top, int bottom, int bands, Color near, Color far)
    {
        if (bottom <= top || bands <= 0)
        {
            return;
        }

        var span = bottom - top;
        for (var i = 0; i < bands; i++)
        {
            var y = top + (span * i / bands);
            var next = top + (span * (i + 1) / bands);
            var t = bands == 1 ? 0f : i / (float)(bands - 1);
            surface.FillRect(0, y, width, next - y, Lerp(near, far, t));
        }
    }

    private static Color Lerp(Color a, Color b, float t) => Color.FromRgb(
        (byte)(a.R + ((b.R - a.R) * t)),
        (byte)(a.G + ((b.G - a.G) * t)),
        (byte)(a.B + ((b.B - a.B) * t)));
}
