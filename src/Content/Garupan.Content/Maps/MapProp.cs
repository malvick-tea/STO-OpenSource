using System.Numerics;

namespace Garupan.Content;

/// <summary>
/// One destructible object placed on a battle map — a row produced by the Python city-prop
/// separator and consumed by the simulation's prop spawner. Carries the ground placement
/// (<see cref="GroundPosition"/> = x east, z north; <see cref="YawRadians"/> facing) and the
/// physical size the destruction physics turns into a resistance. The <see cref="PropKind"/>
/// supplies the material and failure behaviour through <see cref="PropKindCatalog"/>, so a prop
/// row stays as small as "what is it, where, how big".
/// </summary>
public sealed record MapProp(
    PropKind Kind,
    Vector2 GroundPosition,
    float YawRadians,
    float BaseDiameterMeters,
    float HeightMeters);
