using System;
using System.Collections.Generic;

namespace Garupan.Sim;

/// <summary>
/// Accumulator of pending world mutations. Systems enqueue commands during their tick;
/// <see cref="Apply"/> runs once after the last system, in deterministic order
/// (insertion order). This pattern keeps systems read-mostly and makes parallel
/// system execution (planned: post-v1) trivial — no system writes directly to the world.
///
/// Today's implementation is a plain list of delegate-based commands. M3 may swap to
/// a pooled structural-typed command set for zero-alloc enforcement.
/// </summary>
public sealed class CommandBuffer
{
    private readonly List<Action<World>> _pending = new();

    public int PendingCount => _pending.Count;

    public void Defer(Action<World> command) => _pending.Add(command);

    public void Apply(World world)
    {
        for (var i = 0; i < _pending.Count; i++)
        {
            _pending[i](world);
        }

        _pending.Clear();
    }
}
