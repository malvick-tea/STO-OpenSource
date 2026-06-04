namespace Garupan.Content;

/// <summary>
/// Static description of a tank. The fields here are intentionally <i>specs</i> — they
/// describe what the vehicle is, not its dynamic state in a battle. Battle-side state
/// (HP, ammo, repair timers) lives on simulation components and references a TankSpec
/// by <see cref="Id"/>.
///
/// <see cref="DisplayNameKey"/> is a raw localisation key (string), not a typed
/// <c>TranslationKey</c> — Content stays free of the Localisation assembly. UI and Sim
/// resolve the key against an <c>ITranslationCatalog</c> at the point of use.
///
/// <see cref="School"/> is the canonical armored combat� school the vehicle belongs to. Defaults
/// to <see cref="OpponentSchool.PlayerSchool"/> (the player school) so legacy callers that
/// don't yet care about the school field keep working unchanged; tanks that flag a
/// different school override via <c>with { School = OpponentSchool.RivalEcho }</c>.
/// Renderer consumers (school camo tint), AI / commentary lookups, and replay metadata
/// all read this field.
/// </summary>
public sealed record TankSpec(
    string Id,
    string Designation,
    string DisplayNameKey,
    string ModelResPath,
    ArmorProfile Armor,
    GunSpec Gun,
    GunMountSpec GunMount,
    MobilitySpec Mobility,
    int CrewSize)
{
    /// <summary>Canon school this vehicle is associated with. Drives per-school camo
    /// tint, per-school AI personality (M5+), and canon attribution in commentary
    /// + replay metadata.</summary>
    public OpponentSchool School { get; init; } = OpponentSchool.PlayerSchool;
}
