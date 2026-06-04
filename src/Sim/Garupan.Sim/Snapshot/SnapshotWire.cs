namespace Garupan.Sim.Snapshot;

/// <summary>
/// Wire-format constants for the snapshot codec. Mirror
/// <c>svo::protocol::snapshot_codec.h</c> exactly so a C++ recorder and a C# player
/// share bytes — the future replay format and (eventually) the multiplayer snapshot
/// channel both read these constants instead of duplicating literals.
///
/// Frame layout:
/// <code>
/// header (20 bytes):
///   [0..4)    magic "SVOS"
///   [4..8)    protocol version (uint32 LE)
///   [8..16)   tick index (uint64 LE)
///   [16..20)  entity count E (uint32 LE)
///
/// entity row (40 bytes), repeated E times after the header:
///   [ 0.. 4)  network_id (uint32 LE)
///   [ 4.. 8)  position.x (float32 LE)
///   [ 8..12)  position.y (float32 LE)
///   [12..16)  yaw_radians (float32 LE)
///   [16..20)  turret_yaw_radians (float32 LE)
///   [20..24)  state_flags (uint32 LE)
///   [24..28)  barrel_pitch_radians (float32 LE)
///   [28..32)  min_barrel_pitch_radians (float32 LE)
///   [32..36)  max_barrel_pitch_radians (float32 LE)
///   [36..40)  gun_recoil_travel_metres (float32 LE)
///
/// projectile section header (4 bytes):
///   [0..4)    projectile count P (uint32 LE)
///
/// projectile row (49 bytes), repeated P times:
///   [ 0.. 4)  id (uint32 LE)
///   [ 4.. 8)  position.x (float32 LE)
///   [ 8..12)  position.y (float32 LE)
///   [12..16)  velocity.x (float32 LE)
///   [16..20)  velocity.y (float32 LE)
///   [20..21)  family (uint8)
///   [21..25)  visual_height_metres (float32 LE)
///   [25..29)  vertical_velocity_metres_per_second (float32 LE)
///   [29..33)  distance_travelled_metres (float32 LE)
///   [33..37)  launch_position.x (float32 LE)
///   [37..41)  launch_position.y (float32 LE)
///   [41..45)  launch_visual_height_metres (float32 LE)
///   [45..49)  owner_network_id (uint32 LE)
///
/// prop section header (4 bytes):
///   [0..4)    felled-prop count F (uint32 LE)
///
/// prop row (13 bytes), repeated F times:
///   [ 0.. 4)  prop_id (uint32 LE)
///   [ 4.. 5)  state (uint8 — Garupan.Sim.Components.PropState ordinal)
///   [ 5.. 9)  fall_yaw_radians (float32 LE)
///   [ 9..13)  topple_seconds (float32 LE)
/// </code>
/// </summary>
public static class SnapshotWire
{
    /// <summary>Wire protocol version. Bumped any time the layout above changes in a way
    /// the previous decoder cannot parse. 3 adds barrel pitch to tanks and visual muzzle
    /// height to projectiles. 4 adds vertical velocity and travelled distance. 5 adds
    /// immutable projectile launch origin for muzzle effects and replay tooling. 6 adds
    /// per-chassis gun elevation limits for client-side preview clamping. 7 adds
    /// gun-recoil travel and projectile owner ids for visual articulation and local audio.
    /// 8 adds the felled-prop section so destructible street clutter breaks authoritatively
    /// on the client without its geometry crossing the wire.</summary>
    public const uint ProtocolVersion = 8;

    public const int HeaderBytes = 20;
    public const int EntityBytes = 40;
    public const int ProjectileCountBytes = 4;
    public const int ProjectileBytes = 49;
    public const int PropCountBytes = 4;
    public const int PropBytes = 13;

    /// <summary>Magic prefix on every snapshot frame: ASCII "SVOS". Lets a reader reject
    /// non-snapshot bytes early and gives forensic tooling a marker to grep for.</summary>
    public static readonly byte[] Magic = { (byte)'S', (byte)'V', (byte)'O', (byte)'S' };

    /// <summary>Exact serialised size for a given snapshot.</summary>
    public static int EncodedSize(WorldSnapshot snap) =>
        HeaderBytes
        + (snap.Entities.Count * EntityBytes)
        + ProjectileCountBytes
        + (snap.Projectiles.Count * ProjectileBytes)
        + PropCountBytes
        + (snap.Props.Count * PropBytes);
}
