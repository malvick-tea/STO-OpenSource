using System;
using Arch.Core;
using Garupan.Sim.Components;

namespace Garupan.Sim.Spawn;

/// <summary>Restores destructible map props for a fresh match round. Prop transforms stay fixed:
/// destruction changes lifecycle state only, so reset is a cheap authoritative-world pass.</summary>
public static class MapPropRoundReset
{
    public static void RestoreStanding(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var query = new QueryDescription().WithAll<DestructibleProp>();
        world.Raw.Query(in query, (ref DestructibleProp prop) =>
        {
            prop.State = PropState.Standing;
            prop.StateSeconds = 0f;
        });
    }
}
