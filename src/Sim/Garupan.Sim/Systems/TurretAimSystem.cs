using System;
using Arch.Core;
using Garupan.Sim.Components;

namespace Garupan.Sim.Systems;

/// <summary>
/// Rotates each turret toward its <see cref="TurretTarget"/> at <see cref="Turret.TraverseSpeedRadPerS"/>.
/// Shortest-path: takes the (-π, π] delta and steps the smaller side of the circle.
/// Snaps when |delta| ≤ max-step-this-tick to avoid sub-tick oscillation around target.
///
/// Phase 0 stores turret yaw and TurretTarget in the world frame. The renderer derives
/// the local turret-ring rotation by subtracting hull yaw at draw time.
///
/// <see cref="KnockedOut"/> tanks are skipped.
///
/// Order: 400 — runs after hull drive, before reload + fire.
/// Ported from <c>svo/engine/src/systems/turret_aim.cpp</c>.
/// </summary>
public sealed class TurretAimSystem : IFixedSystem
{
    private const float Pi = MathF.PI;
    private const float TwoPi = 2f * MathF.PI;

    public string Name => "TurretAim";

    public int Order => 400;

    public void Tick(in TickContext ctx)
    {
        var dt = (float)ctx.Time.TickIntervalSeconds;
        if (dt < 0f)
        {
            dt = 0f;
        }

        var query = new QueryDescription()
            .WithAll<Turret, TurretTarget>()
            .WithNone<KnockedOut>();

        ctx.World.Raw.Query(in query, (ref Turret turret, ref TurretTarget target) =>
        {
            var delta = WrapSignedPi(target.YawRadians - turret.YawRadians);
            var maxStep = turret.TraverseSpeedRadPerS * dt;
            if (maxStep <= 0f)
            {
                return;
            }

            if (MathF.Abs(delta) <= maxStep)
            {
                turret.YawRadians = WrapSignedPi(target.YawRadians);
            }
            else
            {
                var direction = delta > 0f ? 1f : -1f;
                turret.YawRadians = WrapSignedPi(turret.YawRadians + (direction * maxStep));
            }
        });
    }

    /// <summary>Wraps an arbitrary angle into the half-open range (-π, π].</summary>
    private static float WrapSignedPi(float angle)
    {
        var shifted = angle + Pi;
        var folded = shifted - (MathF.Floor(shifted / TwoPi) * TwoPi);
        return folded - Pi;
    }
}
