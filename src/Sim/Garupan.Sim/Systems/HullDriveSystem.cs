using System;
using Arch.Core;
using Garupan.Sim.Components;
using Opus.Engine.Physics.Ground;

namespace Garupan.Sim.Systems;

/// <summary>
/// Integrates retained force-based ground dynamics for every drivable hull. Catalogue
/// values describe physical inputs; Opus derives speed, acceleration, gear, RPM, and
/// yaw response without a game-side velocity cap. When the match supplies a terrain height
/// sampler the same integrator resolves the local slope, so every tank — the whole roster,
/// no per-model work — seats on the relief, slides downhill, and grips a drivable grade.
/// </summary>
public sealed class HullDriveSystem : IFixedSystem
{
    private readonly GroundVehicleEnvironment _environment;

    /// <summary>Flat-ground hull dynamics — the default everywhere a match has no terrain
    /// (determinism scenarios, canon missions on a flat field).</summary>
    public HullDriveSystem()
        : this(terrainHeightSampler: null)
    {
    }

    /// <summary>Hull dynamics over the supplied terrain: <paramref name="terrainHeightSampler"/>
    /// maps world (x east, z north) to surface height, feeding the integrator's slope force. Null
    /// keeps the flat-ground environment, leaving behaviour (and the determinism digests) identical.</summary>
    public HullDriveSystem(Func<float, float, float>? terrainHeightSampler)
    {
        _environment = GroundVehicleEnvironment.EarthCompactedGround with
        {
            SurfaceHeightSampler = terrainHeightSampler,
        };
    }

    public string Name => "HullDrive";

    public int Order => 300;

    public void Tick(in TickContext ctx)
    {
        var dt = MathF.Max(0f, (float)ctx.Time.TickIntervalSeconds);
        var query = new QueryDescription()
            .WithAll<Transform, Hull, DriveInput>()
            .WithNone<KnockedOut>();

        ctx.World.Raw.Query(in query, (ref Transform tf, ref Hull hull, ref DriveInput input) =>
        {
            if (hull.Dynamics is null)
            {
                return;
            }

            var state = hull.DynamicsState with
            {
                PositionMeters = tf.Position,
                YawRadians = tf.YawRadians,
            };
            state = GroundVehicleIntegrator.Advance(
                state,
                hull.Dynamics,
                _environment,
                new GroundVehicleControls(ClampUnit(input.Throttle), ClampUnit(input.Steering)),
                dt);
            hull.DynamicsState = state;
            tf.Position = state.PositionMeters;
            tf.YawRadians = state.YawRadians;
        });
    }

    private static float ClampUnit(float value) => Math.Clamp(value, -1f, 1f);
}
