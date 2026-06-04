namespace Garupan.Content;

/// <summary>
/// What a destructible map object is, independent of where it stands. The Python map
/// separator classifies each extracted object into one of these from its name and geometry;
/// the simulation maps the kind onto a material and a failure behaviour through
/// <see cref="PropKindCatalog"/>, so a city's clutter needs only a kind + a size to behave.
/// </summary>
public enum PropKind
{
    /// <summary>A rooted tree — a thick trunk that topples when felled, never shatters.</summary>
    Tree,

    /// <summary>A low bush or hedge — light foliage that is brushed flat.</summary>
    Bush,

    /// <summary>A lamp post / utility pole — a tall steel column that bends over.</summary>
    LampPost,

    /// <summary>A traffic light head on its pole.</summary>
    TrafficLight,

    /// <summary>A road sign on a slim post — light, shears off on contact.</summary>
    TrafficSign,

    /// <summary>A fire hydrant — short, stout cast metal.</summary>
    Hydrant,

    /// <summary>A street bin / trash can — hollow, free-standing, scatters.</summary>
    Bin,

    /// <summary>A bench — light street furniture that breaks apart.</summary>
    Bench,

    /// <summary>A wooden crate — splinters on impact.</summary>
    Crate,

    /// <summary>A barrel — light drum that is knocked apart.</summary>
    Barrel,

    /// <summary>A traffic cone / bollard — trivially flattened.</summary>
    Cone,

    /// <summary>A fence section — a light barrier that is flattened.</summary>
    Fence,
}

/// <summary>
/// How a defeated prop fails once an impactor delivers enough energy. Trees and poles are
/// rooted members that hinge over (<see cref="Topple"/>); light or brittle clutter comes apart
/// (<see cref="Break"/>). The threshold to reach failure is the same physics either way
/// (<c>Opus.Engine.Physics.Destruction.PropResistance</c>); only the aftermath differs.
/// </summary>
public enum PropBehavior
{
    /// <summary>Hinges over at the base and lies down — a felled trunk, a bent pole.</summary>
    Topple,

    /// <summary>Comes apart into debris and is removed — a smashed sign, a scattered bin.</summary>
    Break,
}
