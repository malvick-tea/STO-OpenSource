namespace Garupan.Sim.Components;

/// <summary>
/// One frame of intent that <see cref="Systems.ApplyInputsSystem"/> has not yet committed
/// to the authoritative <see cref="DriveInput"/> / <see cref="TurretTarget"/> /
/// <see cref="FireIntent"/> components. Attached to a tank entity by the input source —
/// the local player loop in single-player, the network receive layer in multiplayer —
/// at most once per peer per tick; the most recent intent wins (the sim never replays a
/// stale half-frame).
///
/// Routing inputs through a component instead of mutating authoritative state at receive
/// time keeps the schedule deterministic: every actor's intent is committed inside
/// ApplyInputsSystem, which runs at a fixed point in the per-tick order (band 100 —
/// before AI at 200, drive at 300, aim at 400). A future recorded-input replay therefore
/// only needs to log <c>PendingInput</c> attachments to reproduce a match.
///
/// Frame interpretation:
/// <list type="bullet">
/// <item><description><see cref="Throttle"/> / <see cref="Steering"/> — clamped to [-1, 1] by ApplyInputs before write-through.</description></item>
/// <item><description><see cref="TurretYawRadians"/> — Phase-0 convention is world-frame yaw (matches the existing
///     <see cref="TurretTarget"/> storage; see <see cref="Systems.TurretAimSystem"/>). When the hull/turret
///     frame split lands in M3+, this field swaps to hull-relative in lockstep with TurretTarget.</description></item>
/// <item><description><see cref="BarrelPitchRadians"/> — requested gun elevation, clamped
///     by ApplyInputs to the rendered gun's supported range.</description></item>
/// <item><description><see cref="Flags"/> — bitfield, see <see cref="InputFlags"/>. <see cref="InputFlags.Fire"/> attaches a one-shot
///     <see cref="FireIntent"/>; <see cref="Systems.GunFireSystem"/> consumes it later in the same tick.</description></item>
/// </list>
///
/// Ported from <c>svo/shared/components/pending_input.h</c>.
/// </summary>
public struct PendingInput
{
    /// <summary>Local-source tick the frame was sampled at. Held for replay log alignment;
    /// ApplyInputs itself does not condition behaviour on this value.</summary>
    public ulong Tick;

    public float Throttle;

    public float Steering;

    public float TurretYawRadians;

    public float BarrelPitchRadians;

    public InputFlags Flags;
}

/// <summary>
/// Bitfield of momentary intent bits carried by <see cref="PendingInput.Flags"/>. A
/// bitfield rather than a single enum because more than one action can be live in the
/// same frame — trigger held plus a future smoke-launcher bit, etc.
///
/// Mirrors C++ <c>svo::protocol::input_flag</c>.
/// </summary>
[System.Flags]
public enum InputFlags : uint
{
    None = 0,

    /// <summary>Trigger pulled this frame. ApplyInputs attaches a one-shot
    /// <see cref="FireIntent"/>; GunFire enforces cooldown and silently drops if reloading.</summary>
    Fire = 1u << 0,
}
