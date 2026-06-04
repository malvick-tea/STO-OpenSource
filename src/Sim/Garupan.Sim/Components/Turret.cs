namespace Garupan.Sim.Components;

/// <summary>
/// Turret rotational state. Phase 0 stores yaw in the world frame so input, snapshot,
/// renderer, and gun-fire maths share one convention. Pitch is barrel elevation from
/// the horizontal plane.
///
/// Units:
/// <list type="bullet">
/// <item><description>YawRadians — world-frame yaw; 0 = east (+X).</description></item>
/// <item><description>BarrelPitchRadians — elevation; positive raises the muzzle.</description></item>
/// <item><description>TraverseSpeedRadPerS — maximum rotation rate, radians per second.</description></item>
/// </list>
/// Ported from <c>svo/shared/components/turret.h</c>.
/// </summary>
public struct Turret
{
    public float YawRadians;
    public float BarrelPitchRadians;
    public float TraverseSpeedRadPerS;
}
