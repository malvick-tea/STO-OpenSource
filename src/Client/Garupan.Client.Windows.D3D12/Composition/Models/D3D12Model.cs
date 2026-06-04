using System.Numerics;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Engine.Ui;
using Opus.Foundation.Geometry;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>D3D12-backed <see cref="IModel"/>. Wraps the GPU asset bundle the
/// runtime scene renderer consumes (<see cref="GltfSceneGpuAssets"/>) together with
/// the per-model material atlas — a <see cref="MultiMaterialAtlas"/> so per-primitive
/// material indices route to the correct albedo texture instead of every primitive
/// sampling one flat colour. Disposing releases both the atlas (its SRV heap + every
/// uploaded texture) and every GPU primitive on the bundle.
/// <para>
/// <see cref="Invalid"/> is the no-render placeholder returned by
/// <see cref="D3D12ModelLoader"/> when a path resolves to a missing or unreadable asset
/// — its <see cref="IsValid"/> is false so the renderer drops every draw it receives.
/// </para></summary>
internal sealed class D3D12Model : IModel
{
    private readonly GltfSceneGpuAssets? _assets;
    private readonly IMaterialAtlas? _atlas;
    private bool _disposed;

    private D3D12Model(GltfSceneGpuAssets? assets, IMaterialAtlas? atlas, Aabb bounds)
    {
        _assets = assets;
        _atlas = atlas;
        BoundsMin = bounds.Min;
        BoundsMax = bounds.Max;
    }

    /// <summary>Placeholder for the "load failed" path. Renders as no-op; subsequent
    /// loads at the same virtual path return this instance via the loader cache so
    /// repeated misses don't churn IO.</summary>
    public static D3D12Model Invalid { get; } = new(assets: null, atlas: null, bounds: new Aabb(Vector3.Zero, Vector3.Zero));

    public bool IsValid => _assets is not null && _atlas is not null;

    public Vector3 BoundsMin { get; }

    public Vector3 BoundsMax { get; }

    /// <summary>The GPU-resident asset bundle that <see cref="D3D12ForwardSceneRenderer"/>
    /// consumes. Null on the invalid placeholder.</summary>
    internal GltfSceneGpuAssets? Assets => _assets;

    /// <summary>The per-model material atlas — every glTF material index resolves to
    /// its authored base-colour texture (KHR-spec-gloss diffuse for legacy Godot exports
    /// such as the medium tank). Null on the invalid placeholder.</summary>
    internal IMaterialAtlas? Atlas => _atlas;

    public static D3D12Model From(GltfSceneGpuAssets assets, IMaterialAtlas atlas) =>
        new(assets, atlas, assets.Bounds);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_assets is not null)
        {
            foreach (var prim in _assets.GpuScene.Primitives)
            {
                prim.Vb.Dispose();
                prim.Ib.Dispose();
            }
        }

        _atlas?.Dispose();
        _disposed = true;
    }
}
