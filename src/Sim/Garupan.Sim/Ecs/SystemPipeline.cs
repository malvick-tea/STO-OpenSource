using System.Collections.Generic;
using System.Linq;
using Opus.Foundation;

namespace Garupan.Sim;

/// <summary>
/// Ordered list of systems plus a single shared <see cref="CommandBuffer"/>. Building
/// a pipeline freezes the system order — once <see cref="Tick"/> has run once, adding
/// or reordering systems requires building a new pipeline (which a new replay test
/// will catch via golden-hash mismatch).
/// </summary>
public sealed class SystemPipeline
{
    private readonly ISystem[] _systems;
    private readonly CommandBuffer _commands;

    public SystemPipeline(IEnumerable<ISystem> systems)
    {
        Ensure.NotNull(systems);
        _systems = systems.OrderBy(s => s.Order).ToArray();
        _commands = new CommandBuffer();
    }

    public IReadOnlyList<ISystem> Systems => _systems;

    public CommandBuffer Commands => _commands;

    public void Tick(World world, GameTime time, SimSeed seed)
    {
        var ctx = new TickContext(world, time, seed, _commands);

        for (var i = 0; i < _systems.Length; i++)
        {
            _systems[i].Tick(in ctx);
        }

        _commands.Apply(world);
    }
}
