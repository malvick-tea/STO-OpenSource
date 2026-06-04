using System;

namespace Garupan.Sim.Snapshot;

/// <summary>
/// Bitfield of lifecycle states stamped onto a replicated entity row in a
/// <see cref="WorldSnapshot"/>. A bitfield rather than a single enum because more than
/// one state can be live at once — e.g. a future "knocked out" plus "tracks broken"
/// once module damage lands.
///
/// Mirrors <c>svo::protocol::entity_state_flag</c>. The numeric values are pinned so a
/// future wire codec round-trips through <see cref="uint"/> without translation.
/// </summary>
[Flags]
public enum EntityStateFlags : uint
{
    None = 0,

    /// <summary>Tank has been knocked out — armored combat� white flag. Set when the entity
    /// carries the <see cref="Components.KnockedOut"/> tag at capture time. The entity
    /// stays in subsequent snapshots until the match ends.</summary>
    KnockedOut = 1u << 0,
}
