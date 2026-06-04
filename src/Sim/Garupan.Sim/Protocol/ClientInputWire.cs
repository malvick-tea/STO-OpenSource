namespace Garupan.Sim.Protocol;

/// <summary>
/// Wire layout for <see cref="ClientInputFrame"/>. Fixed-size 40-byte frame.
///
/// Header (8 bytes):
/// <code>
///   [0..4)  magic "SVOI"
///   [4..8)  protocol version (uint32 LE)
/// </code>
///
/// Body (32 bytes):
/// <code>
///   [ 0.. 8)  tick (uint64 LE)
///   [ 8..12)  network_id (uint32 LE)
///   [12..16)  throttle (float32 LE)
///   [16..20)  steering (float32 LE)
///   [20..24)  turret_yaw_radians (float32 LE)
///   [24..28)  flags (uint32 LE)
///   [28..32)  barrel_pitch_radians (float32 LE)
/// </code>
///
/// Mirrors <c>svo::protocol::client_input_codec.h</c>.
/// </summary>
public static class ClientInputWire
{
    public const int HeaderBytes = 8;
    public const int BodyBytes = 32;
    public const int FrameBytes = HeaderBytes + BodyBytes;

    public static readonly byte[] Magic = { (byte)'S', (byte)'V', (byte)'O', (byte)'I' };
}
