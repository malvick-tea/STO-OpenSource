namespace Garupan.Sim.Components;

/// <summary>
/// Strongly-typed identifier for a tank chassis catalogue entry. Backed by a 16-bit int
/// because the catalogue is curated and bounded — there is no scenario in which the
/// roster grows past ~65k entries. The wrapper prevents the perennial "swapped two int
/// arguments" class of bug at API boundaries (Hull, spawning, balance lookups).
///
/// Ported from <c>svo/shared/components/tank_id.h</c> (Tea's Family STO Phase 0).
/// </summary>
public enum TankId : ushort
{
    /// <summary>Sentinel for an uninitialised handle. Catalogue lookups must reject this.</summary>
    None = 0,
}

/// <summary>
/// Strongly-typed identifier for a gun calibre catalogue entry. Bounded set, frequent
/// passing across API boundaries, easy to confuse with adjacent integer fields.
/// Ported from <c>svo/shared/components/tank_id.h</c>.
/// </summary>
public enum CaliberId : ushort
{
    None = 0,
}

/// <summary>
/// Ammunition family — the canonical shell classification used by the catalogue, HUD, and
/// shell visuals. The penetration resolver is <i>table-driven</i> (each round carries a
/// normal-incidence penetration curve resolved geometrically against plate slope), so it does
/// NOT branch on this enum: the kinetic-vs-chemical distinction is expressed by the authored
/// curve shape (chemical rounds are flat versus range), not by per-family code paths.
///
/// <list type="bullet">
/// <item><description>AP — uncapped armour-piercing kinetic shot.</description></item>
/// <item><description>APCR — sub-calibre tungsten-cored kinetic, higher near penetration.</description></item>
/// <item><description>HEAT — shaped-charge chemical, penetration independent of range.</description></item>
/// <item><description>HE — high-explosive, marginal penetration.</description></item>
/// <item><description>APC — armour-piercing capped (penetrating cap, no burster).</description></item>
/// <item><description>APCBC — capped with a ballistic cap (a common armour-piercing shell).</description></item>
/// <item><description>APHE — armour-piercing with an explosive filler and tracer.</description></item>
/// <item><description>HVAP — high-velocity armour-piercing (US sub-calibre, ≈ APCR).</description></item>
/// </list>
///
/// Numeric values are appended, never renumbered: the wire/snapshot byte and the
/// <c>Garupan.Content.AmmoType</c> mirror both depend on 0–3 staying put.
/// Ported from <c>svo/shared/components/ammo.h</c>.
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
