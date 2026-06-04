namespace Garupan.Sim.Protocol;

/// <summary>
/// Wire layout for <see cref="WelcomeFrame"/>. Fixed-size 16-byte frame (Phase 47+).
///
/// Header (8 bytes):
/// <code>
///   [0..4)  magic "SVOW"
///   [4..8)  protocol version (uint32 LE)
/// </code>
///
/// Body (8 bytes):
/// <code>
///   [0..4)  network_id (uint32 LE)
///   [4..5)  team_id (uint8)
///   [5..6)  mode_kind (uint8 — WelcomeMatchModeKind)
///   [6..7)  respawns_configured (uint8)
///   [7..8)  is_commander (uint8 — 0 / 1)
/// </code>
///
/// Mirrors <c>svo::protocol::welcome_codec.h</c>.
/// </summary>
public static class WelcomeWire
{
    public const int HeaderBytes = 8;
    public const int BodyBytes = 8;
    public const int FrameBytes = HeaderBytes + BodyBytes;

    public static readonly byte[] Magic = { (byte)'S', (byte)'V', (byte)'O', (byte)'W' };
}
