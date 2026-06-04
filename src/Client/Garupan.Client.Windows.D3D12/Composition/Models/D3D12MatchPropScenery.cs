using System.Collections.Generic;
using Garupan.Client.Ui.Match.Network;
using Garupan.Content;
using Garupan.Sim.Snapshot;
using Garupan.Sim.Terrain;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Renderer.Direct3D12.Scene;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>
/// Holds a map's destructible-prop layout and turns it into scene draws each frame. Props never move,
/// so every standing box is computed once on construction and cached; per frame only the handful of
/// props the snapshot reports felled are recomputed (hinged over or hidden), and every other prop's
/// cached boxes are reused — the city's thousands of poles and signs are never re-projected when
/// nothing has broken. Each box draws the engine's null-material <see cref="GarageSceneAssets.PropBoxMeshIndex"/>
/// cube, tinted to read as street furniture rather than tank camo.
/// </summary>
/// <remarks>
/// The pure placement maths (scale, ground-seat, topple hinge) lives in <see cref="MatchPropInstances"/>;
/// this class only owns the GPU-facing concerns — the cube mesh slice, the cached draw list, and the
/// per-frame compose. The box mesh is the procedural unit cube, so it shares the cube's half-extent
/// when scaling each prop to its catalogue size.
/// </remarks>
internal sealed class D3D12MatchPropScenery
{
    private readonly IReadOnlyList<MapProp> _layout;
    private readonly IHeightSurface? _terrain;
    private readonly DynamicFelledPropSurface _surface;
    private readonly int _meshIndex;
    private readonly SceneNodeDraw[] _standingDraws;
    private readonly IReadOnlyList<PropBoxSlice> _standingSlices;

    public D3D12MatchPropScenery(IReadOnlyList<MapProp> layout, IHeightSurface? terrain, int propBoxMeshIndex)
    {
        _layout = layout;
        _terrain = terrain;
        _surface = new DynamicFelledPropSurface(terrain is null ? null : terrain.HeightAt);
        _meshIndex = propBoxMeshIndex;
        var standing = MatchPropInstances.BuildStanding(layout, terrain, ProjectileMesh.DefaultHalfExtentMeters);
        _standingDraws = ToDraws(standing.Boxes);
        _standingSlices = standing.SliceByProp;
    }

    /// <summary>Number of cached standing-prop draws — a tight capacity hint for the frame's draw list.</summary>
    public int StandingDrawCount => _standingDraws.Length;

    /// <summary>Surface used to seat tanks this frame: base terrain plus the props reported down.</summary>
    public IHeightSurface? HeightSurfaceFor(IReadOnlyList<PropSnapshot> felled)
    {
        _surface.Clear();
        for (var i = 0; i < felled.Count; i++)
        {
            var snapshot = felled[i];
            if ((uint)snapshot.PropId >= (uint)_layout.Count)
            {
                continue;
            }

            _surface.Add(ToSurfaceMember(_layout[snapshot.PropId], snapshot));
        }

        return _surface.HasMembers || _terrain is not null ? _surface : null;
    }

    /// <summary>Appends this frame's prop draws to <paramref name="draws"/>: the cached standing box(es)
    /// for every intact prop, recomputed felled poses for the few the snapshot reports down, and nothing
    /// for shattered ones. The fast path (no felled props) bulk-copies the cached array.</summary>
    public void AppendDraws(List<SceneNodeDraw> draws, IReadOnlyList<PropSnapshot> felled)
    {
        if (felled.Count == 0)
        {
            draws.AddRange(_standingDraws);
            return;
        }

        var felledById = BuildLookup(felled);
        for (var propId = 0; propId < _layout.Count; propId++)
        {
            if (felledById.TryGetValue(propId, out var snapshot))
            {
                AppendFelled(draws, _layout[propId], snapshot);
            }
            else
            {
                AppendStanding(draws, _standingSlices[propId]);
            }
        }
    }

    private void AppendStanding(List<SceneNodeDraw> draws, PropBoxSlice slice)
    {
        for (var i = 0; i < slice.Count; i++)
        {
            draws.Add(_standingDraws[slice.Start + i]);
        }
    }

    private void AppendFelled(List<SceneNodeDraw> draws, MapProp prop, PropSnapshot felled)
    {
        var boxes = MatchPropInstances.BuildFelled(prop, felled, _terrain, ProjectileMesh.DefaultHalfExtentMeters);
        for (var i = 0; i < boxes.Count; i++)
        {
            draws.Add(new SceneNodeDraw(_meshIndex, boxes[i].World, boxes[i].Tint));
        }
    }

    private static FelledPropSurfaceMember ToSurfaceMember(MapProp prop, PropSnapshot snapshot) =>
        new(
            prop.GroundPosition,
            snapshot.FallYawRadians,
            prop.HeightMeters,
            prop.BaseDiameterMeters * 0.5f,
            snapshot.State);

    private SceneNodeDraw[] ToDraws(IReadOnlyList<MatchPropBoxInstance> boxes)
    {
        var draws = new SceneNodeDraw[boxes.Count];
        for (var i = 0; i < boxes.Count; i++)
        {
            draws[i] = new SceneNodeDraw(_meshIndex, boxes[i].World, boxes[i].Tint);
        }

        return draws;
    }

    private static Dictionary<int, PropSnapshot> BuildLookup(IReadOnlyList<PropSnapshot> felled)
    {
        var lookup = new Dictionary<int, PropSnapshot>(felled.Count);
        for (var i = 0; i < felled.Count; i++)
        {
            lookup[felled[i].PropId] = felled[i];
        }

        return lookup;
    }
}
