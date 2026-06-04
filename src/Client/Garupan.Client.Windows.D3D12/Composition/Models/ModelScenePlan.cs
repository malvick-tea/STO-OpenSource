using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Engine.Renderer;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Ui;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>Pure, GPU-free translation of the screen-facing 3D draw contract into the
/// inputs the runtime scene renderer consumes. <see cref="D3D12ModelRenderer"/>
/// accumulates <see cref="IModelRenderer"/> calls during a frame, then delegates here to
/// turn the backend-agnostic <see cref="CameraView3D"/> into a <see cref="FrameCameraSet"/>
/// and the per-model placements into one flat <see cref="SceneNodeDraw"/> draw list.
/// <para>
/// Separated from the renderer so the projection maths + node-graph transform composition
/// are unit-testable without a D3D12 device — the renderer itself can only be exercised
/// through a GPU smoke test.
/// </para></summary>
internal static class ModelScenePlan
{
    /// <summary>Near plane, in metres. Tight enough to frame a tank on a garage pedestal
    /// without clipping the nearest hull plate.</summary>
    private const float NearPlaneMeters = 0.1f;

    /// <summary>Far plane, in metres. Generous headroom over any single-tank framing
    /// distance; the scene renderer's depth target is 32-bit float so the wide range
    /// costs no precision worth reclaiming.</summary>
    private const float FarPlaneMeters = 500f;

    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Builds the single-main-camera <see cref="FrameCameraSet"/> a scene frame
    /// needs from a backend-agnostic <see cref="CameraView3D"/> and the pixel size of the
    /// offscreen viewport (which fixes the aspect ratio).</summary>
    public static FrameCameraSet BuildCameras(in CameraView3D camera, int viewportWidth, int viewportHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(viewportWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(viewportHeight);

        var aspect = viewportWidth / (float)viewportHeight;
        var fovYRadians = camera.FovYDegrees * DegreesToRadians;
        var view = Matrix4x4.CreateLookAt(camera.Position, camera.Target, camera.Up);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(fovYRadians, aspect, NearPlaneMeters, FarPlaneMeters);
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        return FrameCameraSet.SingleMain(new CameraSetup(
            View: view,
            Projection: projection,
            PositionWorld: camera.Position,
            ForwardWorld: forward,
            NearPlane: NearPlaneMeters,
            FarPlane: FarPlaneMeters,
            FovYRadians: fovYRadians,
            AspectRatio: aspect));
    }

    /// <summary>Converts a 0..255 <see cref="Color"/> tint into the 0..1 RGBA factor the
    /// scene renderer multiplies onto each node's resolved base colour.</summary>
    public static Vector4 ToTintFactor(Color tint) =>
        new(tint.R / 255f, tint.G / 255f, tint.B / 255f, tint.A / 255f);

    /// <summary>Flattens every placement's glTF node-graph template into one draw list:
    /// each template node's local world is post-multiplied by the placement's world
    /// transform and its tint factor multiplied by the placement tint. Output is
    /// placement-major then template-node order — a stable, deterministic submission
    /// sequence.</summary>
    public static SceneNodeDraw[] FlattenNodeDraws(IReadOnlyList<ModelPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);

        var result = new SceneNodeDraw[CountNodeDraws(placements)];
        var write = 0;
        for (var i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            var templates = placement.NodeTemplates;
            for (var n = 0; n < templates.Count; n++)
            {
                var template = templates[n];
                result[write++] = new SceneNodeDraw(
                    template.MeshIndex,
                    template.World * placement.World,
                    template.TintFactor * placement.Tint);
            }
        }

        return result;
    }

    private static int CountNodeDraws(IReadOnlyList<ModelPlacement> placements)
    {
        var total = 0;
        for (var i = 0; i < placements.Count; i++)
        {
            total += placements[i].NodeTemplates.Count;
        }

        return total;
    }
}
