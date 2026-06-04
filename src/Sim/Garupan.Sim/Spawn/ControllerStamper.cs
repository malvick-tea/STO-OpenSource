using Garupan.Sim.Components;

namespace Garupan.Sim.Spawn;

/// <summary>
/// Attaches the controller tag(s) for a freshly-spawned tank according to its
/// <see cref="TankControl"/> kind. Knows nothing about the spec or world position —
/// just the per-entity routing of player / AI / inert affiliations.
/// </summary>
public static class ControllerStamper
{
    /// <summary>Phase-0 default engage range for an AI tank with no explicit
    /// <see cref="BotBrain"/> argument. Matches the C++ reference (60 m).</summary>
    public const float DefaultBotEngageRangeMeters = 60f;

    public static void Stamp(World world, EntityHandle entity, TankControl control, BotBrain? botBrain)
    {
        switch (control)
        {
            case TankControl.Player:
                world.Add(entity, default(PlayerControlled));
                break;

            case TankControl.AiBot:
                world.Add(entity, default(AiControlled));
                world.Add(entity, botBrain ?? new BotBrain { EngageRangeMeters = DefaultBotEngageRangeMeters });
                break;

            case TankControl.None:
            default:
                // Inert: no controller component attached. Useful for scripted entities
                // that exist only to be shot at, or for replay-only ghosts.
                break;
        }
    }
}
