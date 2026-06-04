using ArchWorld = Arch.Core.World;

namespace Garupan.Sim;

#pragma warning disable CA1716 // 'World' clashes with VB.NET reserved word; we accept the friction.

/// <summary>
/// Wrapper over <see cref="ArchWorld"/> giving Sim a stable internal API while keeping
/// Arch as an implementation detail. Consumers (Sim systems, Game) reference
/// <c>Garupan.Sim.World</c>; the Arch package is opaque to them.
///
/// Disposable so worlds can be torn down at match-end without leaking native Arch buffers.
/// </summary>
public sealed class World : System.IDisposable
{
    private readonly ArchWorld _inner;

    private World(ArchWorld inner)
    {
        _inner = inner;
    }

    /// <summary>Creates a fresh world. Allocates Arch internal chunks; do once per match.</summary>
    public static World Create()
    {
        var inner = ArchWorld.Create();
        return new World(inner);
    }

    /// <summary>Backdoor for systems that legitimately need the underlying Arch world.</summary>
    public ArchWorld Raw => _inner;

    public EntityHandle CreateEntity()
    {
        var e = _inner.Create();
        return new EntityHandle(e);
    }

    public void Destroy(EntityHandle handle) => _inner.Destroy(handle.Raw);

    public bool IsAlive(EntityHandle handle) => _inner.IsAlive(handle.Raw);

    public int EntityCount => _inner.Size;

    /// <summary>Creates an entity with one component value. Returns its handle.</summary>
    public EntityHandle Spawn<T>(in T value)
        where T : struct
    {
        var e = _inner.Create(value);
        return new EntityHandle(e);
    }

    /// <summary>Creates an entity with two component values.</summary>
    public EntityHandle Spawn<T1, T2>(in T1 a, in T2 b)
        where T1 : struct
        where T2 : struct
    {
        var e = _inner.Create(a, b);
        return new EntityHandle(e);
    }

    /// <summary>Creates an entity with three component values.</summary>
    public EntityHandle Spawn<T1, T2, T3>(in T1 a, in T2 b, in T3 c)
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        var e = _inner.Create(a, b, c);
        return new EntityHandle(e);
    }

    public bool Has<T>(EntityHandle handle) => _inner.Has<T>(handle.Raw);

    public ref T Get<T>(EntityHandle handle) => ref _inner.Get<T>(handle.Raw);

    public void Set<T>(EntityHandle handle, in T value) => _inner.Set(handle.Raw, value);

    /// <summary>Attaches a tag/component to an entity. No-op if it already exists.</summary>
    public void Add<T>(EntityHandle handle, in T value)
    {
        if (!_inner.Has<T>(handle.Raw))
        {
            _inner.Add(handle.Raw, value);
        }
    }

    /// <summary>Detaches a component. No-op if not present.</summary>
    public void Remove<T>(EntityHandle handle)
    {
        if (_inner.Has<T>(handle.Raw))
        {
            _inner.Remove<T>(handle.Raw);
        }
    }

    /// <summary>Add (if missing) or overwrite (if present). The "fire and forget" set.</summary>
    public void AddOrSet<T>(EntityHandle handle, in T value)
    {
        if (_inner.Has<T>(handle.Raw))
        {
            _inner.Set(handle.Raw, value);
        }
        else
        {
            _inner.Add(handle.Raw, value);
        }
    }

    public void Dispose() => ArchWorld.Destroy(_inner);
}

#pragma warning restore CA1716
