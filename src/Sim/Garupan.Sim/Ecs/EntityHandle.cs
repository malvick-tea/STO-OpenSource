using System.Runtime.InteropServices;
using ArchEntity = Arch.Core.Entity;

namespace Garupan.Sim;

/// <summary>
/// Stable handle to an entity in a <see cref="World"/>. Wraps Arch's <c>Entity</c> so
/// callers don't take a transitive dependency on the Arch package — they only see types
/// in <c>Garupan.Sim</c>.
///
/// Generation / liveness checking is delegated to Arch via <see cref="World.IsAlive"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct EntityHandle : System.IEquatable<EntityHandle>
{
    internal readonly ArchEntity Raw;

    internal EntityHandle(ArchEntity raw)
    {
        Raw = raw;
    }

    public int Id => Raw.Id;

    public int WorldId => Raw.WorldId;

    public bool Equals(EntityHandle other) => Raw.Id == other.Raw.Id && Raw.WorldId == other.Raw.WorldId;

    public override bool Equals(object? obj) => obj is EntityHandle h && Equals(h);

    public override int GetHashCode() => System.HashCode.Combine(Raw.Id, Raw.WorldId);

    public static bool operator ==(EntityHandle a, EntityHandle b) => a.Equals(b);

    public static bool operator !=(EntityHandle a, EntityHandle b) => !a.Equals(b);

    public override string ToString() => $"e#{Raw.Id}@w{Raw.WorldId}";
}
