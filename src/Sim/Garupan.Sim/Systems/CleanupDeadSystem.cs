using System.Collections.Generic;
using Arch.Core;
using Garupan.Sim.Components;

namespace Garupan.Sim.Systems;

/// <summary>
/// Reaps entities tagged <see cref="Dead"/> at end-of-tick. Last in the pipeline so
/// every other system's view of the world is consistent for the duration of one tick;
/// only after all systems have run do we destroy.
///
/// Two-phase collect-then-destroy pattern same as the other mutating systems —
/// destroying mid-iteration is unsafe across Arch versions.
///
/// Order: 900 — last in the canonical pipeline (per ISystem.Order conventions).
/// Ported from <c>svo/engine/src/systems/cleanup_dead.cpp</c>.
/// </summary>
public sealed class CleanupDeadSystem : IFixedSystem
{
    public string Name => "CleanupDead";

    public int Order => 900;

    public void Tick(in TickContext ctx)
    {
        var raw = ctx.World.Raw;
        var doomed = new List<Entity>();

        var query = new QueryDescription().WithAll<Dead>();
        raw.Query(in query, (Entity e) => doomed.Add(e));

        foreach (var e in doomed)
        {
            raw.Destroy(e);
        }
    }
}
