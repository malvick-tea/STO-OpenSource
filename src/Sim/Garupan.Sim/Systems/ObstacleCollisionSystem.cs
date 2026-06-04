using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Garupan.Sim.Collision;
using Garupan.Sim.Components;

namespace Garupan.Sim.Systems;

/// <summary>
/// Resolves tanks against impassable static obstacles — building footprints, walls, piers. An
/// obstacle is a solid oriented box no tank can drive through nor destroy, so every contact is a
/// block: the hull is pushed back out and the velocity driving it into the face is cancelled, while
/// the sliding component survives so a tank scrubs along a wall. The whole roster collides with the
/// whole city through one pure geometry routine (<see cref="CircleBoxCollision"/>) and one shared
/// response (<see cref="HullContactResponse"/>), with no per-tank or per-building tuning.
/// </summary>
/// <remarks>
/// Order 340 — after the hull has been integrated to its new position (300) and before destructible
/// props (350) and aim (400), so the hull is first stopped by anything solid, then tested against
/// the lighter clutter it can fell. The broad phase is the naive tank×obstacle scan over a reused
/// list — adequate for present city counts; a spatial grid is the documented follow-up shared with
/// <see cref="PropCollisionSystem"/>.
/// </remarks>
public sealed class ObstacleCollisionSystem : IFixedSystem
{
    /// <summary>Pipeline position: after hull integration (300), before prop collision (350).</summary>
    public const int PipelineOrder = 340;

    /// <summary>Reused between ticks so the obstacle scan does not allocate per frame.</summary>
    private readonly List<ObstacleContact> _obstacles = new();

    public string Name => "ObstacleCollision";

    public int Order => PipelineOrder;

    public void Tick(in TickContext ctx)
    {
        var world = ctx.World;
        CollectObstacles(world);
        if (_obstacles.Count == 0)
        {
            return;
        }

        ResolveTankContacts(world);
    }

    private void CollectObstacles(World world)
    {
        _obstacles.Clear();
        var obstacles = _obstacles;
        var query = new QueryDescription().WithAll<Transform, StaticObstacle>();
        world.Raw.Query(in query, (ref Transform tf, ref StaticObstacle obstacle) =>
        {
            obstacles.Add(new ObstacleContact(tf.Position, obstacle));
        });
    }

    private void ResolveTankContacts(World world)
    {
        var obstacles = _obstacles;
        var query = new QueryDescription().WithAll<Transform, Hull, HitRadius>().WithNone<KnockedOut>();
        world.Raw.Query(in query, (ref Transform tf, ref Hull hull, ref HitRadius radius) =>
        {
            // Sequential resolution: each separation moves the hull, so the next obstacle is tested
            // from where it actually ended up — a tank wedged in a corner is freed from both faces.
            for (var i = 0; i < obstacles.Count; i++)
            {
                var obstacle = obstacles[i];
                var contact = CircleBoxCollision.Resolve(
                    tf.Position,
                    radius.Meters,
                    obstacle.Center,
                    obstacle.Data.LocalRight,
                    obstacle.Data.LocalForward,
                    obstacle.Data.HalfExtents);
                if (contact.Overlaps)
                {
                    HullContactResponse.Separate(ref tf, ref hull, contact.PushDirection, contact.Depth);
                }
            }
        });
    }

    private readonly record struct ObstacleContact(Vector2 Center, StaticObstacle Data);
}
