using System.Numerics;

namespace Garupan.Client.Ui.Match;

/// <summary>
/// Pure value type carrying the on-screen rectangle of the top-down match viewport plus
/// the world half-extent it maps to. Owns the world ↔ screen coordinate conversions so
/// callers (renderers, input adapters) don't reimplement the same arithmetic per layer.
///
/// Convention: screen Y grows down, world Y grows up (north). The transforms flip Y so
/// "north on the map" stays "up on the screen". X is not flipped — east is east.
///
/// Stays an immutable record so a frame can capture the viewport once at the start of
/// Render and pass it to every drawer without worrying about it shifting mid-frame.
/// </summary>
public readonly record struct MatchViewport(int X, int Y, int Size, float HalfExtentMeters)
{
    public int Width => Size;

    public int Height => Size;

    public float PixelsPerMeter => Size / (HalfExtentMeters * 2f);

    /// <summary>Pick the largest square viewport that fits inside <paramref name="surfaceWidth"/>
    /// × <paramref name="surfaceHeight"/> with the configured top-bar / HUD-strip insets.</summary>
    public static MatchViewport Fit(int surfaceWidth, int surfaceHeight, float halfExtentMeters)
    {
        const int HudStripWidth = 360;
        const int VerticalInset = 200;
        const int OriginX = 40;
        const int OriginY = 80;
        var size = System.Math.Min(surfaceWidth - HudStripWidth, surfaceHeight - VerticalInset);
        if (size < 0)
        {
            size = 0;
        }

        return new MatchViewport(OriginX, OriginY, size, halfExtentMeters);
    }

    public (int X, int Y) WorldToScreen(Vector2 world)
    {
        var sx = X + (int)((world.X + HalfExtentMeters) * PixelsPerMeter);
        var sy = Y + Size - (int)((world.Y + HalfExtentMeters) * PixelsPerMeter);
        return (sx, sy);
    }

    public Vector2 ScreenToWorld(int sx, int sy)
    {
        var wx = ((sx - X) / PixelsPerMeter) - HalfExtentMeters;
        var wy = HalfExtentMeters - ((sy - Y) / PixelsPerMeter);
        return new Vector2(wx, wy);
    }

    public bool Contains(int sx, int sy) =>
        sx >= X && sx < X + Size && sy >= Y && sy < Y + Size;
}
