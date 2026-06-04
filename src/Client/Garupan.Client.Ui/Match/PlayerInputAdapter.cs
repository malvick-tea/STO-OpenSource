using Garupan.Client.Core.Application;
using Garupan.Sim.Components;
using Opus.Engine.Input;

namespace Garupan.Client.Ui.Match;

/// <summary>
/// Writes one frame of player command onto the ECS player tank. Called by
/// <see cref="MatchScreen"/> before each pipeline tick so the player's commands and the
/// AI's commands both land on the same tick boundary.
///
/// Keyboard movement + fire are resolved against the active <see cref="InputBindings"/>
/// by <see cref="PlayerMovementIntent.Read"/>; this adapter only translates that intent
/// into <see cref="DriveInput"/> / <see cref="FireIntent"/> components. Turret aim follows
/// the mouse — the screen converts the cursor to a world-frame yaw and feeds it back
/// through <see cref="ApplyAim"/>.
/// </summary>
public sealed class PlayerInputAdapter
{
    public void ApplyMovement(MatchSession session, IInputSource input, InputBindings bindings)
    {
        var intent = PlayerMovementIntent.Read(input, bindings);
        session.World.AddOrSet(session.Player, new DriveInput { Throttle = intent.Throttle, Steering = intent.Steering });

        if (intent.Fire)
        {
            session.World.Add(session.Player, default(FireIntent));
        }
    }

    public void ApplyAim(MatchSession session, float worldYawRadians)
    {
        session.World.AddOrSet(session.Player, new TurretTarget { YawRadians = worldYawRadians });
    }
}
