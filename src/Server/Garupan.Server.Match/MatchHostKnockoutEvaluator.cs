using Garupan.Sim;
using Garupan.Sim.Components;

namespace Garupan.Server.Match;

/// <summary>Pure predicate: "is this tank finally knocked out?" — i.e. permanently
/// removed from the match for outcome-tracking purposes. A respawning tank (active
/// <see cref="RespawnTimer"/>) is NOT finally knocked out; nor is a tank that still
/// carries respawn budget on its <see cref="RespawnLives"/>. A tank without any
/// respawn component is finally knocked out the moment <see cref="KnockedOut"/>
/// arrives, matching the legacy Phase-0 single-life semantic.</summary>
internal static class MatchHostKnockoutEvaluator
{
    public static bool IsFinallyKnockedOut(World world, EntityHandle entity)
    {
        if (!world.Has<KnockedOut>(entity))
        {
            return false;
        }

        if (world.Has<RespawnTimer>(entity))
        {
            return false;
        }

        if (!world.Has<RespawnLives>(entity))
        {
            return true;
        }

        return world.Get<RespawnLives>(entity).Remaining == 0;
    }
}
