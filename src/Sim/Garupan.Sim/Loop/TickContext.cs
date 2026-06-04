using Opus.Foundation;

namespace Garupan.Sim;

/// <summary>
/// Read-only context handed to every system on each fixed-step tick. Carries the world,
/// the current tick number / clock, the seed, and a command buffer for deferred mutations.
///
/// Systems MUST NOT mutate the world directly — they enqueue intent on
/// <see cref="Commands"/>. The pipeline applies all commands in a single pass after the
/// last system runs, so order-of-mutation is deterministic.
/// </summary>
public readonly ref struct TickContext
{
    public TickContext(World world, GameTime time, SimSeed seed, CommandBuffer commands)
    {
        World = world;
        Time = time;
        Seed = seed;
        Commands = commands;
    }

    public World World { get; }

    public GameTime Time { get; }

    public SimSeed Seed { get; }

    public CommandBuffer Commands { get; }
}
