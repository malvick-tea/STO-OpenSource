using FluentAssertions;
using Garupan.Sim;
using Xunit;

namespace Garupan.Sim.Tests.Ecs;

public sealed class WorldTests
{
    [Fact]
    public void Create_returns_empty_world()
    {
        using var world = World.Create();
        world.EntityCount.Should().Be(0);
    }

    [Fact]
    public void CreateEntity_increments_count()
    {
        using var world = World.Create();
        var e1 = world.CreateEntity();
        var e2 = world.CreateEntity();

        world.EntityCount.Should().Be(2);
        e1.Should().NotBe(e2);
        world.IsAlive(e1).Should().BeTrue();
        world.IsAlive(e2).Should().BeTrue();
    }

    [Fact]
    public void Destroy_drops_entity()
    {
        using var world = World.Create();
        var e = world.CreateEntity();
        world.Destroy(e);

        world.IsAlive(e).Should().BeFalse();
        world.EntityCount.Should().Be(0);
    }
}
