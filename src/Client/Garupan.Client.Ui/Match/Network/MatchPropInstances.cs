using System;
using System.Collections.Generic;
using System.Numerics;
using Garupan.Content;
using Garupan.Sim.Components;
using Garupan.Sim.Snapshot;
using Garupan.Sim.Systems;
using Garupan.Sim.Terrain;
using Opus.Foundation;

namespace Garupan.Client.Ui.Match.Network;

/// <summary>One blockout box ready to submit to the 3D scene: its world transform and the RGBA tint
/// multiplied onto the white material slot. A prop yields one or two of these (a tree adds a canopy).</summary>
public readonly record struct MatchPropBoxInstance(Matrix4x4 World, Vector4 Tint);

/// <summary>The contiguous run of boxes that draw one prop within a <see cref="StandingPropScenery"/>,
/// so a felled prop can be overridden by copying every other prop's cached boxes and recomputing only
/// the few that fell — never rebuilding the whole city's clutter each frame.</summary>
public readonly record struct PropBoxSlice(int Start, int Count);

/// <summary>Every prop drawn upright, plus the per-prop box slices keyed by prop id (= layout index).
/// Built once when a map loads because props never move; only their felled state changes.</summary>
public sealed record StandingPropScenery(
    IReadOnlyList<MatchPropBoxInstance> Boxes,
    IReadOnlyList<PropBoxSlice> SliceByProp);

/// <summary>
/// Pure projection of a map's static prop layout plus the authoritative felled-prop set into the
/// blockout boxes the match renderer draws — no GPU, no ECS, so it is unit-testable headless and the
/// D3D12 renderer stays a thin shell. A standing prop is an upright box (or boxes); a felled one
/// hinges over toward its impact heading by the authoritative topple fraction, or vanishes when it
/// shattered (<see cref="PropState.Broken"/>). The client owns the full layout and overrides only the
/// props the snapshot reports felled, so a tank smashing a pole shows it break with no prop geometry
/// ever crossing the wire.
/// </summary>
public static class MatchPropInstances
{
    /// <summary>Topple angle below which a prop is treated as upright (no hinge transform).</summary>
    private const float ToppleAngleEpsilon = 1e-4f;

    private static readonly float HalfPi = MathF.PI / 2f;

    /// <summary>Every prop in <paramref name="layout"/> upright, with the per-prop slice index. The
    /// prop id is the layout index — the same order the server spawned from the shared CSV.</summary>
    public static StandingPropScenery BuildStanding(
        IReadOnlyList<MapProp> layout,
        IHeightSurface? terrain,
        float unitHalfExtentMeters)
    {
        Ensure.NotNull(layout);
        var boxes = new List<MatchPropBoxInstance>(layout.Count);
        var slices = new PropBoxSlice[layout.Count];
        for (var i = 0; i < layout.Count; i++)
        {
            var start = boxes.Count;
            AppendLayers(boxes, layout[i], terrain, unitHalfExtentMeters, toppleAngle: 0f, fallYaw: 0f);
            slices[i] = new PropBoxSlice(start, boxes.Count - start);
        }

        return new StandingPropScenery(boxes, slices);
    }

    /// <summary>The visible boxes for one felled prop: hinged over by the authoritative topple
    /// fraction for a toppling/fallen member, or empty when it shattered and is hidden.</summary>
    public static IReadOnlyList<MatchPropBoxInstance> BuildFelled(
        MapProp prop,
        PropSnapshot felled,
        IHeightSurface? terrain,
        float unitHalfExtentMeters)
    {
        Ensure.NotNull(prop);
        if (felled.State == PropState.Broken)
        {
            return Array.Empty<MatchPropBoxInstance>();
        }

        var boxes = new List<MatchPropBoxInstance>(2);
        var angle = ResolveToppleAngle(felled.State, felled.ToppleSeconds);
        AppendLayers(boxes, prop, terrain, unitHalfExtentMeters, angle, felled.FallYawRadians);
        return boxes;
    }

    private static void AppendLayers(
        List<MatchPropBoxInstance> boxes,
        MapProp prop,
        IHeightSurface? terrain,
        float unitHalfExtentMeters,
        float toppleAngle,
        float fallYaw)
    {
        var profile = PropVisualCatalog.For(prop.Kind);
        var baseRadius = prop.BaseDiameterMeters * 0.5f;
        var x = prop.GroundPosition.X;
        var z = prop.GroundPosition.Y;
        var groundY = terrain?.HeightAt(x, z) ?? 0f;
        var placement = PropPlacement(toppleAngle, fallYaw, x, groundY, z);
        foreach (var layer in profile.Layers)
        {
            var world = LayerLocal(layer, baseRadius, prop.HeightMeters, unitHalfExtentMeters) * placement;
            boxes.Add(new MatchPropBoxInstance(world, layer.Tint));
        }
    }

    /// <summary>Maps one box layer onto the unit cube: scales it to the layer's footprint + vertical
    /// span and lifts it so the prop's base sits at local Y=0 (the hinge line), ready for the prop
    /// placement to anchor + topple it.</summary>
    private static Matrix4x4 LayerLocal(PropBoxLayer layer, float baseRadius, float height, float unitHalfExtentMeters)
    {
        var footprintHalf = layer.RadiusMultiplier * baseRadius;
        var bottom = layer.BottomFraction * height;
        var top = layer.TopFraction * height;
        var scaleXz = footprintHalf / unitHalfExtentMeters;
        var scaleY = (top - bottom) * 0.5f / unitHalfExtentMeters;
        var centerY = (bottom + top) * 0.5f;
        return Matrix4x4.CreateScale(scaleXz, scaleY, scaleXz)
            * Matrix4x4.CreateTranslation(0f, centerY, 0f);
    }

    /// <summary>Anchors the prop at its ground position and, when felled, hinges it about its base
    /// toward the impact heading. The hinge axis is the horizontal perpendicular to the fall
    /// direction, so the standing member tips over the way the tank was travelling.</summary>
    private static Matrix4x4 PropPlacement(float toppleAngle, float fallYaw, float x, float groundY, float z)
    {
        var anchor = Matrix4x4.CreateTranslation(x, groundY, z);
        if (toppleAngle <= ToppleAngleEpsilon)
        {
            return anchor;
        }

        var hingeAxis = new Vector3(MathF.Sin(fallYaw), 0f, -MathF.Cos(fallYaw));
        return Matrix4x4.CreateFromAxisAngle(hingeAxis, toppleAngle) * anchor;
    }

    private static float ResolveToppleAngle(PropState state, float toppleSeconds) => state switch
    {
        PropState.Toppling => Math.Clamp(toppleSeconds / PropCollisionSystem.ToppleDurationSeconds, 0f, 1f) * HalfPi,
        PropState.Fallen => HalfPi,
        _ => 0f,
    };
}
