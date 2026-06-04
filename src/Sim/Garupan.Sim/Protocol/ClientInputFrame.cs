using Garupan.Sim.Components;

namespace Garupan.Sim.Protocol;

/// <summary>
/// One frame of player intent travelling from client to server. Authoritative writes
/// happen server-side; this is a declaration of intent that the server commits via
/// <see cref="Systems.ApplyInputsSystem"/>.
///
/// Mirrors <c>svo::protocol::ClientInputFrame</c>. Phase-0 has no rollback / client-side
/// prediction outside trivial extrapolation; introducing those is a Phase-1 concern.
///
/// Phase-0 turret yaw convention matches the in-process <see cref="PendingInput"/>:
/// the field is world-frame today, flips to hull-relative in lockstep with
/// <see cref="TurretTarget"/> when the hull/turret frame split lands in M3+.
/// </summary>
public readonly record struct ClientInputFrame(
    ulong Tick,
    uint NetworkId,
    float Throttle,
    float Steering,
    float TurretYawRadians,
    InputFlags Flags,
    float BarrelPitchRadians = 0f);
