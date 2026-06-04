using Arch.Core;
using Garupan.Sim.Components;

namespace Garupan.Sim.Systems;

/// <summary>
/// Counts down each entity's <see cref="Lifetime"/>. When the budget reaches zero, tags
/// the entity <see cref="Dead"/> for end-of-tick cleanup. Used for projectiles that fly
/// off the map without hitting anything.
///
/// Order: 700 — runs after hit resolve, before cleanup.
/// Ported from <c>svo/engine/src/systems/lifetime_decay.cpp</c>.
/// </summary>
public sealed class LifetimeDecaySystem : IFixedSystem
{
    public string Name => "LifetimeDecay";

    public int Order => 700;

    public void Tick(in TickContext ctx)
    {
        var dt = (float)ctx.Time.TickIntervalSeconds;
        if (dt < 0f)
        {
            dt = 0f;
        }

        var raw = ctx.World.Raw;
        var query = new QueryDescription()
            .WithAll<Lifetime>()
            .WithNone<Dead>();

        // Two-phase: collect, then mutate. Adding Dead components mid-iteration could
        // shuffle archetypes under us; same pattern as ProjectileHitResolveSystem.
        var doomed = new System.Collections.Generic.List<Entity>();

        raw.Query(in query, (Entity e, ref Lifetime lifetime) =>
        {
            lifetime.SecondsRemaining -= dt;
            if (lifetime.SecondsRemaining <= 0f)
            {
                lifetime.SecondsRemaining = 0f;
                doomed.Add(e);
            }
        });

        foreach (var e in doomed)
        {
            if (!raw.Has<Dead>(e))
            {
                raw.Add(e, default(Dead));
            }
        }
    }
}
