namespace Garupan.Sim.Components;

/// <summary>
/// Per-tank vertical hit geometry, stamped at spawn from the chassis <c>body_height</c> so the
/// hit resolver no longer assumes one silhouette for every vehicle. <see cref="HeightMeters"/> is
/// the top of the struck volume — a round passing above it misses; <see cref="HullTurretSplitMeters"/>
/// is the height below which an impact resolves against the hull band and above which against the
/// turret band. A 2.16 m AssaultGun and a 3.25 m heavy tank D no longer share a flat 3 m / 1.5 m guess.
/// </summary>
public struct Silhouette
{
    public float HeightMeters;
    public float HullTurretSplitMeters;
}
