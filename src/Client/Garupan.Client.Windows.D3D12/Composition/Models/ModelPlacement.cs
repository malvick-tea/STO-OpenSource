using System.Collections.Generic;
using System.Numerics;
using Opus.Engine.Renderer.Direct3D12.Assets;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>One model placed in the scene for a single frame: the model's flattened glTF
/// node-graph template plus the world transform and tint the screen requested through
/// <see cref="D3D12ModelRenderer.DrawModel"/> / <see cref="D3D12ModelRenderer.DrawModelEx"/>.
/// Holds no GPU handles so <see cref="ModelScenePlan.FlattenNodeDraws"/> stays a pure,
/// device-free, unit-testable function.</summary>
internal readonly record struct ModelPlacement(
    IReadOnlyList<SceneNodeDraw> NodeTemplates,
    Matrix4x4 World,
    Vector4 Tint);
