using System.Numerics;

namespace Garupan.Content;

/// <summary>
/// One impassable static obstacle on a battle map — a building footprint, a wall, a pier: a solid
/// rectangular volume a tank can never drive through nor knock down. Distinct from
/// <see cref="MapProp"/> (light, circular clutter that a charging hull fells): an obstacle is a
/// large, immovable, oriented box, so the collision response is "block and slide", never "destroy".
/// </summary>
/// <remarks>
/// The footprint is an oriented rectangle on the ground plane: centred at <see cref="Center"/>
/// (x east, z north), rotated by <see cref="YawRadians"/> (CCW from +X), with half-extents
/// <see cref="HalfWidthMeters"/> along its local right axis and <see cref="HalfDepthMeters"/> along
/// its local forward axis. <see cref="HeightMeters"/> is descriptive — it is the wall height the
/// generator placed and is reserved for future line-of-sight / cover (Pillar 2); the 2-D hull
/// collision uses only the footprint. A row is produced by the city generator alongside the visual
/// mesh, so the colliders stay aligned with what the renderer draws with no per-map code.
/// </remarks>
public sealed record MapObstacle(
    Vector2 Center,
    float YawRadians,
    float HalfWidthMeters,
    float HalfDepthMeters,
    float HeightMeters);
