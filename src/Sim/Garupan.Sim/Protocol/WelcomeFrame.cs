namespace Garupan.Sim.Protocol;

/// <summary>Mode kind discriminant on the welcome wire. Matches the byte stamped at the
/// <c>mode_kind</c> position of <see cref="WelcomeWire"/>'s body. Mirrors
/// <see cref="Garupan.Content.MatchModeKind"/> in shape, but lives in the Sim Protocol
/// tier so the Sim assembly does NOT take a Content reference.</summary>
public enum WelcomeMatchModeKind : byte
{
    /// <summary>Every vehicle for themselves: Hungry Battles.</summary>
    FreeForAll = 0,

    /// <summary>Two opposing teams with a commander: Tactical 5v5.</summary>
    TeamTactical = 1,
}

/// <summary>
/// Server-to-client greeting sent immediately after the peer's CONNECT event is accepted.
/// Tells the client which network id corresponds to the vehicle the server already spawned
/// for it, which team it was balanced into, and what kind of match it joined.
///
/// Without this frame the client would be blind to which row in every subsequent snapshot
/// is "me" because every replicated entity looks the same on the wire. WelcomeFrame closes
/// that gap with the smallest possible payload.
///
/// Mirrors <c>svo::protocol::WelcomeFrame</c>.
/// </summary>
/// <param name="NetworkId">Stable network id stamped on the local peer's vehicle.</param>
/// <param name="TeamId">Armored combat team the peer was placed on (matches <see cref="Garupan.Sim.Components.Team"/>).</param>
/// <param name="ModeKind">Match-mode discriminant used by client labels and respawn-HUD copy.</param>
/// <param name="RespawnsConfigured">Initial respawn budget the server stamped on the peer's vehicle.</param>
/// <param name="IsCommander">True when the server designated this peer the commander of its team.</param>
public readonly record struct WelcomeFrame(
    uint NetworkId,
    byte TeamId,
    WelcomeMatchModeKind ModeKind,
    byte RespawnsConfigured,
    bool IsCommander);
