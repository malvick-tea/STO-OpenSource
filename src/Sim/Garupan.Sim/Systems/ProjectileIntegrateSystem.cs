using System.Numerics;
using Arch.Core;
using Garupan.Sim.Components;
using Opus.Engine.Physics.Ballistics;

namespace Garupan.Sim.Systems;

/// <summary>
/// Advances exterior ballistics before gunfire so a newly spawned round remains at its
/// muzzle for the first snapshot. Runtime rounds use Opus RK4 atmosphere, drag,
/// gravity, and ground intersection; hand-built debug rounds without a physical profile
/// keep a linear fallback.
/// </summary>
public sealed class ProjectileIntegrateSystem : IFixedSystem
{
    private static readonly BallisticEnvironment Environment = BallisticEnvironment.EarthSeaLevel;

    public string Name => "ProjectileIntegrate";

    public int Order => 460;

    public void Tick(in TickContext ctx)
    {
        var dt = MathF.Max(0f, (float)ctx.Time.TickIntervalSeconds);
        var query = new QueryDescription().WithAll<Transform, Projectile>();
        ctx.World.Raw.Query(in query, (ref Transform transform, ref Projectile projectile) =>
        {
            projectile.PreviousPosition = transform.Position;
            projectile.PreviousVisualHeightMeters = projectile.VisualHeightMeters;
            projectile.HasIntegratedSegment = true;
            if (projectile.Dynamics is null)
            {
                transform.Position += projectile.Velocity * dt;
                return;
            }

            var initial = new BallisticState(
                new Vector3(transform.Position.X, projectile.VisualHeightMeters, transform.Position.Y),
                new Vector3(projectile.Velocity.X, projectile.VerticalVelocityMps, projectile.Velocity.Y),
                projectile.FlightSeconds,
                projectile.DistanceTravelledMeters);
            var result = BallisticIntegrator.Advance(initial, projectile.Dynamics, Environment, dt);
            transform.Position = new Vector2(result.State.PositionMeters.X, result.State.PositionMeters.Z);
            projectile.VisualHeightMeters = result.State.PositionMeters.Y;
            projectile.Velocity = new Vector2(result.State.VelocityMps.X, result.State.VelocityMps.Z);
            projectile.VerticalVelocityMps = result.State.VelocityMps.Y;
            projectile.FlightSeconds = result.State.ElapsedSeconds;
            projectile.DistanceTravelledMeters = result.State.DistanceTravelledMeters;
            projectile.HitGround = result.HitGround;
        });
    }
}
