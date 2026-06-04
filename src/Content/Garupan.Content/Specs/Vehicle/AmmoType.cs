namespace Garupan.Content;

/// <summary>
/// Ammunition family — data-side mirror of <c>Garupan.Sim.Components.AmmoType</c>. The
/// numeric values are pinned to match so the spawn pipeline (which lives in Sim and
/// reads catalogue rows from Content) can <c>(Sim.AmmoType)content.Type</c> with a plain
/// cast and no lookup table.
///
/// Mirroring the enum across the Sim ↔ Content layer matches the C++ pattern: there
/// <c>svo::shared::components::AmmoType</c> (runtime/ECS) and <c>svo::protocol::ProjectileFamily</c>
/// (wire) share numeric values for the same "no translation tax across layers" reason.
/// Adding a family is a two-line change in both enums; the test asserts they stay in lock-step.
/// Values are appended, never renumbered — 0–3 are pinned by the wire/snapshot byte.
///
/// The families are catalogue/HUD/shell-visual labels: the penetration resolver is table-driven
/// (a per-round normal-incidence curve resolved against plate slope), so the kinetic-vs-chemical
/// distinction lives in the authored curve shape, not in per-family branches. See
/// <c>Garupan.Sim.Components.AmmoType</c> for the family descriptions.
/// </summary>
public enum AmmoType : byte
{
    AP = 0,
    APCR = 1,
    HEAT = 2,
    HE = 3,
    APC = 4,
    APCBC = 5,
    APHE = 6,
    HVAP = 7,
}
