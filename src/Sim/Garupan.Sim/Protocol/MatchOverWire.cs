namespace Garupan.Sim.Protocol;

/// <summary>
/// Wire layout for <see cref="MatchOverFrame"/>. Fixed-size 17-byte frame.
///
/// Header (8 bytes):
/// <code>
///   [0..4)  magic "SVOO"
///   [4..8)  protocol version (uint32 LE)
/// </code>
///
/// Body (9 bytes):
/// <code>
///   [0..4)  result (uint32 LE — <see cref="MatchOverResult"/> discriminant)
///   [4..8)  winner_network_id (uint32 LE)
///   [8..9)  winner_team (uint8)
/// </code>
/// </summary>
public static class MatchOverWire
{
    public const int HeaderBytes = 8;
    public const int BodyBytes = 9;
    public const int FrameBytes = HeaderBytes + BodyBytes;

    public static readonly byte[] Magic = { (byte)'S', (byte)'V', (byte)'O', (byte)'O' };
}
