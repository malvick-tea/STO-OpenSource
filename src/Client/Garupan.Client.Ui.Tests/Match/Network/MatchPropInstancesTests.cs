using System.Numerics;
using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Garupan.Content;
using Garupan.Sim.Components;
using Garupan.Sim.Snapshot;
using Garupan.Sim.Systems;
using Garupan.Sim.Terrain;
using Xunit;

namespace Garupan.Client.Ui.Tests.Match.Network;

/// <summary>
/// Pure coverage for <see cref="MatchPropInstances"/>: a static prop layout becomes upright blockout
/// boxes seated on the ground; a felled prop hinges over toward its impact heading or vanishes when
/// shattered. No GPU — the maths is verified by transforming the box's local top point into the
/// world and asserting where the prop's tip ends up.
/// </summary>
public sealed class MatchPropInstancesTests
{
    private const float UnitHalf = 0.2f;

    /// <summary>The prop's local top point is the unit cube's top-centre — its world position after a
    /// box transform is where the prop's tip is, the cleanest probe for upright vs felled.</summary>
    private static Vector3 Tip(MatchPropBoxInstance box) =>
        Vector3.Transform(new Vector3(0f, UnitHalf, 0f), box.World);

    private static MapProp LampPost(float x, float z, float height = 9f) =>
        new(PropKind.LampPost, new Vector2(x, z), 0f, 0.18f, height);

    [Fact]
    public void A_standing_prop_is_one_upright_box_seated_on_the_ground()
    {
        var scenery = MatchPropInstances.BuildStanding(new[] { LampPost(10f, 20f) }, terrain: null, UnitHalf);

        scenery.Boxes.Should().ContainSingle();
        scenery.SliceByProp.Should().ContainSingle().Which.Should().Be(new PropBoxSlice(0, 1));

        // Box centre sits at half height (base on the ground, top at full height).
        scenery.Boxes[0].World.Translation.Should().Be(new Vector3(10f, 4.5f, 20f));
        Tip(scenery.Boxes[0]).Should().BeEquivalentTo(
            new Vector3(10f, 9f, 20f), opts => opts.Using<float>(c => c.Subject.Should().BeApproximately(c.Expectation, 1e-3f)).WhenTypeIs<float>());
    }

    [Fact]
    public void The_prop_id_is_the_layout_index_so_each_prop_owns_a_slice()
    {
        var scenery = MatchPropInstances.BuildStanding(
            new[] { LampPost(0f, 0f), LampPost(5f, 0f), LampPost(10f, 0f) }, terrain: null, UnitHalf);

        scenery.SliceByProp.Should().Equal(
            new PropBoxSlice(0, 1), new PropBoxSlice(1, 1), new PropBoxSlice(2, 1));
    }

    [Fact]
    public void A_tree_draws_a_trunk_and_a_canopy()
    {
        var tree = new MapProp(PropKind.Tree, new Vector2(0f, 0f), 0f, 0.55f, 9f);

        var scenery = MatchPropInstances.BuildStanding(new[] { tree }, terrain: null, UnitHalf);

        scenery.Boxes.Should().HaveCount(2, "a tree is a trunk box plus a wider canopy box");
        scenery.SliceByProp[0].Should().Be(new PropBoxSlice(0, 2));
    }

    [Fact]
    public void A_fallen_prop_lies_along_its_impact_heading()
    {
        // Fall heading 0 rad = +X (east); a felled 9 m pole's tip lands ~9 m east of its base, flat.
        var felled = new PropSnapshot(PropId: 0, State: PropState.Fallen, FallYawRadians: 0f, ToppleSeconds: 0.8f);

        var boxes = MatchPropInstances.BuildFelled(LampPost(10f, 20f), felled, terrain: null, UnitHalf);

        var tip = Tip(boxes[0]);
        tip.X.Should().BeApproximately(19f, 1e-2f);
        tip.Y.Should().BeApproximately(0f, 1e-2f);
        tip.Z.Should().BeApproximately(20f, 1e-2f);
    }

    [Fact]
    public void A_toppling_prop_hinges_partway_over()
    {
        // Half the topple duration → ~45°: the tip is east of the base and still above the ground.
        var halfway = new PropSnapshot(
            PropId: 0,
            State: PropState.Toppling,
            FallYawRadians: 0f,
            ToppleSeconds: PropCollisionSystem.ToppleDurationSeconds * 0.5f);

        var tip = Tip(MatchPropInstances.BuildFelled(LampPost(10f, 0f), halfway, terrain: null, UnitHalf)[0]);

        tip.X.Should().BeInRange(10.5f, 18.5f);
        tip.Y.Should().BeInRange(1f, 8f);
    }

    [Fact]
    public void A_shattered_prop_is_hidden()
    {
        var broken = new PropSnapshot(PropId: 0, State: PropState.Broken, FallYawRadians: 0f, ToppleSeconds: 0f);

        MatchPropInstances.BuildFelled(new MapProp(PropKind.Bin, Vector2.Zero, 0f, 0.4f, 1f), broken, terrain: null, UnitHalf)
            .Should().BeEmpty();
    }

    [Fact]
    public void Prop_boxes_carry_an_opaque_non_black_tint_from_the_catalogue()
    {
        // Guards the static-init ordering in PropVisualCatalog: a colour read before assignment would
        // come through as default(Vector4) — transparent black — and the prop would vanish.
        var box = MatchPropInstances.BuildStanding(new[] { LampPost(0f, 0f) }, terrain: null, UnitHalf).Boxes[0];

        box.Tint.W.Should().Be(1f, "props render opaque");
        (box.Tint.X + box.Tint.Y + box.Tint.Z).Should().BeGreaterThan(0.1f, "the catalogue tint must be initialised");
    }

    [Fact]
    public void Standing_props_seat_on_the_terrain_surface()
    {
        var terrain = new TerrainHeightField(2, 100f, new[] { 5f, 5f, 5f, 5f });

        var box = MatchPropInstances.BuildStanding(new[] { LampPost(0f, 0f) }, terrain, UnitHalf).Boxes[0];

        // Base lifted onto the 5 m surface → centre at 5 + height/2.
        box.World.Translation.Y.Should().BeApproximately(9.5f, 1e-3f);
    }
}
