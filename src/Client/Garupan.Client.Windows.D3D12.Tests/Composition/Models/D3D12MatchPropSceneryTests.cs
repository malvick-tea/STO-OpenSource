using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Garupan.Client.Windows.Direct3D12.Composition.Models;
using Garupan.Content;
using Garupan.Sim.Components;
using Garupan.Sim.Snapshot;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Xunit;

namespace Garupan.Client.Windows.Direct3D12.Tests.Composition.Models;

/// <summary>
/// Headless coverage for <see cref="D3D12MatchPropScenery"/>'s cache + felled-override compose — the
/// D3D12-side logic that reuses cached standing draws for intact props and recomputes only the few
/// the snapshot reports felled. No GPU: it asserts on the produced <see cref="SceneNodeDraw"/> list.
/// </summary>
public sealed class D3D12MatchPropSceneryTests
{
    private const int PropBoxMeshIndex = 7;

    private static MapProp LampPost(float x, float z) =>
        new(PropKind.LampPost, new Vector2(x, z), 0f, 0.18f, 9f);

    [Fact]
    public void With_no_felled_props_every_prop_renders_its_cached_standing_box()
    {
        var scenery = new D3D12MatchPropScenery(
            new[] { LampPost(0f, 0f), LampPost(5f, 0f) }, terrain: null, PropBoxMeshIndex);

        scenery.StandingDrawCount.Should().Be(2);

        var draws = new List<SceneNodeDraw>();
        scenery.AppendDraws(draws, Array.Empty<PropSnapshot>());

        draws.Should().HaveCount(2);
        draws.Should().OnlyContain(d => d.MeshIndex == PropBoxMeshIndex);
    }

    [Fact]
    public void A_shattered_prop_is_dropped_while_the_others_keep_standing()
    {
        var scenery = new D3D12MatchPropScenery(
            new[] { LampPost(0f, 0f), LampPost(5f, 0f), LampPost(10f, 0f) }, terrain: null, PropBoxMeshIndex);

        var draws = new List<SceneNodeDraw>();
        scenery.AppendDraws(draws, new[] { new PropSnapshot(1, PropState.Broken, 0f, 0f) });

        draws.Should().HaveCount(2, "the broken prop (id 1) is hidden; the other two stay standing");
    }

    [Fact]
    public void A_toppling_prop_still_renders_a_box_at_its_felled_pose()
    {
        var scenery = new D3D12MatchPropScenery(new[] { LampPost(0f, 0f) }, terrain: null, PropBoxMeshIndex);
        var standing = Snapshot(scenery, Array.Empty<PropSnapshot>()).Single().World;

        var felled = Snapshot(scenery, new[] { new PropSnapshot(0, PropState.Toppling, 1f, 0.4f) }).Single().World;

        felled.Should().NotBe(standing, "a toppling prop hinges over, so its transform differs from upright");
    }

    [Fact]
    public void A_felled_prop_set_builds_a_drive_over_height_surface()
    {
        var scenery = new D3D12MatchPropScenery(new[] { LampPost(10f, 20f) }, terrain: null, PropBoxMeshIndex);

        var surface = scenery.HeightSurfaceFor(new[] { new PropSnapshot(0, PropState.Fallen, 0f, 0.8f) });

        surface.Should().NotBeNull();
        surface!.HeightAt(14f, 20f).Should().BeGreaterThan(0f);
    }

    private static List<SceneNodeDraw> Snapshot(D3D12MatchPropScenery scenery, IReadOnlyList<PropSnapshot> felled)
    {
        var draws = new List<SceneNodeDraw>();
        scenery.AppendDraws(draws, felled);
        return draws;
    }
}
