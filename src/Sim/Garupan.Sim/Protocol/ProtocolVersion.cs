namespace Garupan.Sim.Protocol;

/// <summary>
/// Shared wire-protocol version stamped on every framed message (snapshots, welcome
/// frames, client input frames). Single source of truth so a future bump is one edit
/// instead of three.
///
/// Mirrors <c>svo::protocol::WIRE_PROTOCOL_VERSION</c>. History documented there:
/// <list type="bullet">
/// <item><description>1 — initial Phase-0 layout (welcome / snapshot / client_input).</description></item>
/// <item><description>2 — Phase H grows the snapshot frame with a projectile section after the entity section.</description></item>
/// <item><description>3 — Phase 44 (local test): the welcome body gains <c>mode_kind</c>
///     + <c>respawns_configured</c> bytes so clients can label the match they joined.</description></item>
/// <item><description>4 — Phase 47 (local test): the welcome body gains an
///     <c>is_commander</c> byte so a Tactical 5v5 peer knows it holds the commander role.</description></item>
/// <item><description>5 — match input gains barrel pitch for authoritative muzzle placement.</description></item>
/// </list>
/// </summary>
public static class ProtocolVersion
{
    public const uint Wire = 5;
}
