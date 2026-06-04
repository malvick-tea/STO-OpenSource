using Garupan.Client.Core.Application;
using Garupan.Client.Ui.Match;
using Garupan.Sim.Components;
using Garupan.Sim.Protocol;
using Garupan.Sim.Snapshot;
using Opus.Engine.Input;

namespace Garupan.Client.Ui.Screens.Match;

/// <summary>
/// Pure helper that turns one frame of keyboard input plus a locally-maintained turret
/// target into a
/// <see cref="ClientInputFrame"/> ready to send to the server. No ECS, no transport —
/// the network match screen owns those concerns and this helper is unit-testable
/// headless.
/// </summary>
internal static class NetworkMatchInputCapture
{
    public static ClientInputFrame BuildInputFrame(
        ulong nextInputTick,
        uint localNetworkId,
        IInputSource input,
        InputBindings bindings,
        float turretTargetYawRadians,
        float barrelPitchRadians = 0f)
    {
        var intent = PlayerMovementIntent.Read(input, bindings);
        var flags = intent.Fire ? InputFlags.Fire : InputFlags.None;
        return new ClientInputFrame(
            Tick: nextInputTick,
            NetworkId: localNetworkId,
            Throttle: intent.Throttle,
            Steering: intent.Steering,
            TurretYawRadians: turretTargetYawRadians,
            Flags: flags,
            BarrelPitchRadians: barrelPitchRadians);
    }

    /// <summary>Returns the entity row in <paramref name="snapshot"/> whose Id matches
    /// <paramref name="localNetworkId"/>; null when the local peer isn't in the snapshot
    /// (e.g. the very first frame before the server's first broadcast).</summary>
    public static EntitySnapshot? FindSelf(WorldSnapshot? snapshot, uint localNetworkId)
    {
        if (snapshot is null || localNetworkId == 0)
        {
            return null;
        }

        foreach (var entity in snapshot.Entities)
        {
            if ((uint)entity.Id == localNetworkId)
            {
                return entity;
            }
        }

        return null;
    }
}
