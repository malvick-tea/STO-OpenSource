namespace Garupan.Client.Ui.Commander;

/// <summary>
/// One vertex of a stroke on the commander's hand-drawn map, in screen pixels. Integer
/// because the underlying <see cref="Opus.Engine.Ui.IDrawSurface"/> draws with int
/// coordinates — keeping the storage type matched to the draw API avoids a per-vertex
/// rounding in the render loop. When per-client resolution becomes a thing (multiplayer,
/// post-alpha) strokes will switch to a normalised map-space coordinate; the
/// <see cref="CommanderMapLayout"/> seam absorbs that change without touching the input
/// or renderer signatures.
/// </summary>
public readonly record struct CommanderMapPoint(int X, int Y);
