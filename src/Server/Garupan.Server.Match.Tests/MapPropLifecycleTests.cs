using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using FluentAssertions;
using Garupan.Content;
using Garupan.Sim.Components;
using Opus.Net.Loopback;
using Xunit;

namespace Garupan.Server.Match.Tests;

public sealed class MapPropLifecycleTests
{
    private static readonly MapProp Tree = new(
        PropKind.Tree,
        new Vector2(4f, 5f),
        YawRadians: 0.25f,
        BaseDiameterMeters: 0.3f,
        HeightMeters: 9f);

    private static readonly MapObstacle Building = new(
        new Vector2(20f, -10f),
        YawRadians: 0f,
        HalfWidthMeters: 12f,
        HalfDepthMeters: 8f,
        HeightMeters: 30f);

    [Fact]
    public void Construction_spawns_configured_map_props()
    {
        using var hub = LoopbackTransportHub.Create();
        using var host = CreateHost(hub);

        ReadSingleProp(host).State.Should().Be(PropState.Standing);
    }

    [Fact]
    public void Reset_restores_destroyed_map_props_for_the_next_round()
    {
        using var hub = LoopbackTransportHub.Create();
        using var host = CreateHost(hub);
        BreakSingleProp(host);

        host.ResetMatch();

        var restored = ReadSingleProp(host);
        restored.State.Should().Be(PropState.Standing);
        restored.StateSeconds.Should().Be(0f);
    }

    [Fact]
    public void Construction_spawns_configured_static_obstacles()
    {
        using var hub = LoopbackTransportHub.Create();
        using var host = CreateHost(hub);

        CountObstacles(host).Should().Be(1);
    }

    [Fact]
    public void Reset_keeps_static_obstacles_in_place()
    {
        using var hub = LoopbackTransportHub.Create();
        using var host = CreateHost(hub);

        host.ResetMatch();

        CountObstacles(host).Should().Be(1, "buildings are immovable — a round reset neither drops nor respawns them");
    }

    private static MatchHost CreateHost(LoopbackTransportHub hub) =>
        new(
            hub,
            new MatchHostOptions(
                TankRoster.VehicleMediumA,
                Team.PlayerSchool,
                Vector2.Zero)
            {
                MapProps = new[] { Tree },
                MapObstacles = new[] { Building },
            });

    private static int CountObstacles(MatchHost host)
    {
        var count = 0;
        var query = new QueryDescription().WithAll<StaticObstacle>();
        host.World.Raw.Query(in query, (ref StaticObstacle _) => count++);
        return count;
    }

    private static DestructibleProp ReadSingleProp(MatchHost host)
    {
        var props = new List<DestructibleProp>();
        var query = new QueryDescription().WithAll<DestructibleProp>();
        host.World.Raw.Query(in query, (ref DestructibleProp prop) => props.Add(prop));
        props.Should().ContainSingle();
        return props[0];
    }

    private static void BreakSingleProp(MatchHost host)
    {
        var query = new QueryDescription().WithAll<DestructibleProp>();
        host.World.Raw.Query(in query, (ref DestructibleProp prop) =>
        {
            prop.State = PropState.Broken;
            prop.StateSeconds = 1f;
        });
    }
}
