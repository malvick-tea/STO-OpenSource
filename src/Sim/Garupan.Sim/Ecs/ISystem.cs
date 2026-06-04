namespace Garupan.Sim;

/// <summary>
/// Base contract for any simulation system. Systems run in a deterministic order
/// driven by <see cref="Order"/>; ties broken by registration order.
/// </summary>
public interface ISystem
{
    /// <summary>Stable display name for logs / diagnostic dumps.</summary>
    string Name { get; }

    /// <summary>Lower runs first. Reserved bands match the canonical pipeline:
    /// 100 Input, 200 AI, 300 Mobility, 400 Aim, 500 Ballistics, 600 Hit/Damage,
    /// 700 Match, 800 Narrative, 900 Cleanup. Use intermediate values to slot
    /// new systems without renumbering.</summary>
    int Order { get; }

    /// <summary>Runs once per tick. Hot-path: zero allocations in runtime.</summary>
    void Tick(in TickContext ctx);
}

/// <summary>
/// Marker interface for systems that participate in the deterministic fixed-step loop.
/// Every <see cref="IFixedSystem"/> MUST be zero-allocation in runtime builds —
/// enforced by analyzer GS040 (planned: M2-M3) and by BenchmarkDotNet gate (CI).
/// </summary>
public interface IFixedSystem : ISystem
{
}
