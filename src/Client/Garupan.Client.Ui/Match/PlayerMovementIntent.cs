using Garupan.Client.Core.Application;
using Opus.Engine.Input;

namespace Garupan.Client.Ui.Match;

/// <summary>
/// The keyboard half of one frame of player command — throttle, steering, and a fire
/// edge — resolved from an <see cref="IInputSource"/> against the active
/// <see cref="InputBindings"/>. Turret aim is mouse-driven and lives on
/// <see cref="PlayerInputAdapter.ApplyAim"/>, not here.
/// </summary>
/// <remarks>
/// A pure value with no ECS dependency, so binding resolution is unit-testable without
/// standing up a <see cref="MatchSession"/> world.
/// </remarks>
public readonly record struct PlayerMovementIntent(float Throttle, float Steering, bool Fire)
{
    /// <summary>
    /// Resolves one frame against <paramref name="bindings"/>. Throttle and steering are
    /// level-triggered (held this frame); fire is edge-triggered (rising edge this frame)
    /// so one keypress chambers exactly one round. Opposing keys held together cancel.
    /// </summary>
    public static PlayerMovementIntent Read(IInputSource input, InputBindings bindings)
    {
        var throttle = 0f;
        if (input.IsKeyDown(bindings.MoveForward))
        {
            throttle += 1f;
        }

        if (input.IsKeyDown(bindings.MoveBackward))
        {
            throttle -= 1f;
        }

        var steering = 0f;
        if (input.IsKeyDown(bindings.SteerLeft))
        {
            steering -= 1f;
        }

        if (input.IsKeyDown(bindings.SteerRight))
        {
            steering += 1f;
        }

        return new PlayerMovementIntent(throttle, steering, input.IsKeyPressed(bindings.Fire));
    }
}
