namespace Garupan.Client.Ui.Match;

/// <summary>
/// Geometry the renderer needs to draw the gunsight: where the player tank projects to on
/// screen, where the aim point (mouse) projects to, and the real-world distance between
/// them. The screen computes this once per frame from the live session and viewport.
/// </summary>
public readonly record struct ReticleAim(
    int PlayerScreenX, int PlayerScreenY,
    int AimScreenX, int AimScreenY,
    float RangeMeters);
