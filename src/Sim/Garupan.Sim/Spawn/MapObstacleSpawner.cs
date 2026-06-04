using System;
using System.Collections.Generic;
using System.Numerics;
using Garupan.Content;
using Garupan.Sim.Components;

namespace Garupan.Sim.Spawn;

/// <summary>
/// Turns a map's <see cref="MapObstacle"/> rows into impassable static-obstacle entities. The
/// footprint's world-space axes are derived once, here, from the spawn yaw so the per-tick collision
/// scan never recomputes trig — and so adding building colliders to a map stays pure data.
/// </summary>
public static class MapObstacleSpawner
{
    public static void Spawn(World world, IEnumerable<MapObstacle> obstacles)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(obstacles);
        foreach (var obstacle in obstacles)
        {
            SpawnOne(world, obstacle);
        }
    }

    public static EntityHandle SpawnOne(World world, MapObstacle obstacle)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(obstacle);

        var cos = MathF.Cos(obstacle.YawRadians);
        var sin = MathF.Sin(obstacle.YawRadians);
        var component = new StaticObstacle
        {
            HalfExtents = new Vector2(obstacle.HalfWidthMeters, obstacle.HalfDepthMeters),
            LocalRight = new Vector2(cos, sin),
            LocalForward = new Vector2(-sin, cos),
        };

        return world.Spawn(new Transform(obstacle.Center, obstacle.YawRadians), component);
    }
}
