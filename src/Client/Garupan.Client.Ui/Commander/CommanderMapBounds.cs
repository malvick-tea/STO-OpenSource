namespace Garupan.Client.Ui.Commander;

/// <summary>
/// Rectangular region of the surface owned by the commander's map. Computed by
/// <see cref="CommanderMapLayout"/> once per frame and threaded through the input bridge
/// + the renderer so both sides agree on where the paper is.
///
/// <see cref="Contains"/> uses half-open intervals — the same convention as
/// <see cref="Garupan.Client.Ui.Match.MatchViewport.Contains"/> — so a click on the
/// rightmost or bottom pixel of an adjacent panel doesn't bleed into the map.
/// </summary>
public readonly record struct CommanderMapBounds(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Contains(int px, int py) =>
        px >= X && px < X + Width && py >= Y && py < Y + Height;
}
