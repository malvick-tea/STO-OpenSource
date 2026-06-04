using System.Numerics;
using FluentAssertions;
using Garupan.Sim;
using Garupan.Sim.Components;
using Xunit;

namespace Garupan.Sim.Tests.Systems;

/// <summary>
/// Sanity probes for World.Spawn — pin down whether Arch stores ctor-passed values
/// across multi-component archetypes, and whether Add shifts components correctly.
/// </summary>
public sealed class WorldSpawnSanityTests
{
    [Fact]
    public void Spawn_with_three_components_preserves_all_values()
    {
        using var world = World.Create();
        var e = world.Spawn(
            new Transform(new Vector2(10f, 20f), 1.5f),
            new Turret { YawRadians = 0.7f },
            new Gun { ReloadSeconds = 3.4f });

        world.Get<Transform>(e).Position.Should().Be(new Vector2(10f, 20f));
        world.Get<Transform>(e).YawRadians.Should().Be(1.5f);
        world.Get<Turret>(e).YawRadians.Should().Be(0.7f);
        world.Get<Gun>(e).ReloadSeconds.Should().Be(3.4f);
    }

    [Fact]
    public void Add_after_spawn_does_not_clobber_existing_values()
    {
        using var world = World.Create();
        var e = world.Spawn(
            new Transform(new Vector2(10f, 20f), 1.5f),
            new Turret { YawRadians = 0.7f },
            new Gun { ReloadSeconds = 3.4f });

        world.Add(e, default(FireIntent));

        world.Get<Transform>(e).Position.Should().Be(new Vector2(10f, 20f));
        world.Get<Turret>(e).YawRadians.Should().Be(0.7f);
        world.Get<Gun>(e).ReloadSeconds.Should().Be(3.4f);
        world.Has<FireIntent>(e).Should().BeTrue();
    }

    [Fact]
    public void Remove_after_add_preserves_other_component_values()
    {
        using var world = World.Create();
        var e = world.Spawn(
            new Transform(new Vector2(10f, 20f), 1.5f),
            new Turret { YawRadians = 0.7f },
            new Gun { ReloadSeconds = 3.4f });

        world.Add(e, default(FireIntent));
        world.Remove<FireIntent>(e);

        world.Has<FireIntent>(e).Should().BeFalse();
        world.Get<Transform>(e).Position.Should().Be(new Vector2(10f, 20f));
        world.Get<Turret>(e).YawRadians.Should().Be(0.7f);
        world.Get<Gun>(e).ReloadSeconds.Should().Be(3.4f);
    }
}
