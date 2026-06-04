using System.Collections.Generic;
using FluentAssertions;
using Garupan.Sim;
using Opus.Foundation;
using Xunit;

namespace Garupan.Sim.Tests.Ecs;

public sealed class SystemPipelineTests
{
    private sealed class CountingSystem : ISystem
    {
        public CountingSystem(string name, int order, List<string> log)
        {
            Name = name;
            Order = order;
            _log = log;
        }

        private readonly List<string> _log;

        public string Name { get; }

        public int Order { get; }

        public int TickCount { get; private set; }

        public void Tick(in TickContext ctx)
        {
            TickCount++;
            _log.Add(Name);
        }
    }

    [Fact]
    public void Pipeline_runs_systems_in_order_ascending()
    {
        var log = new List<string>();
        var first = new CountingSystem("first", 100, log);
        var middle = new CountingSystem("middle", 500, log);
        var last = new CountingSystem("last", 900, log);

        // Register in scrambled order — pipeline must sort by Order.
        var pipeline = new SystemPipeline(new ISystem[] { last, first, middle });

        using var world = World.Create();
        pipeline.Tick(world, GameTime.AtRate(60), SimSeed.Zero);

        log.Should().Equal("first", "middle", "last");
        first.TickCount.Should().Be(1);
        middle.TickCount.Should().Be(1);
        last.TickCount.Should().Be(1);
    }

    [Fact]
    public void Pipeline_dispatches_command_buffer_after_all_systems()
    {
        using var world = World.Create();

        // System enqueues a Create; another system observes EntityCount before Apply.
        var enqueueSystem = new EnqueueCreate();
        var observeSystem = new Observer();

        var pipeline = new SystemPipeline(new ISystem[] { enqueueSystem, observeSystem });
        pipeline.Tick(world, GameTime.AtRate(60), SimSeed.Zero);

        // Observer ran AFTER enqueue but BEFORE Apply, so it must see 0 entities.
        observeSystem.SeenCount.Should().Be(0);

        // Apply runs after all systems → world has 1 entity now.
        world.EntityCount.Should().Be(1);
    }

    private sealed class EnqueueCreate : ISystem
    {
        public string Name => "enqueue";

        public int Order => 100;

        public void Tick(in TickContext ctx)
        {
            ctx.Commands.Defer(w => w.CreateEntity());
        }
    }

    private sealed class Observer : ISystem
    {
        public string Name => "observer";

        public int Order => 200;

        public int SeenCount { get; private set; }

        public void Tick(in TickContext ctx) => SeenCount = ctx.World.EntityCount;
    }
}
