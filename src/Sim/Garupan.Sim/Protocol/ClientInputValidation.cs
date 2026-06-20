using Garupan.Sim.Components;

namespace Garupan.Sim.Protocol;

public static class ClientInputValidation
{
    private const float MinimumAxisValue = -1f;
    private const float MaximumAxisValue = 1f;
    private const InputFlags AllowedFlags = InputFlags.Fire;

    public static bool IsValid(ClientInputFrame frame) =>
        float.IsFinite(frame.Throttle)
        && float.IsFinite(frame.Steering)
        && float.IsFinite(frame.TurretYawRadians)
        && float.IsFinite(frame.BarrelPitchRadians)
        && frame.Throttle is >= MinimumAxisValue and <= MaximumAxisValue
        && frame.Steering is >= MinimumAxisValue and <= MaximumAxisValue
        && (frame.Flags & ~AllowedFlags) == 0;
}
