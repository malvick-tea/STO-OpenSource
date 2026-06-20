using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Opus.Engine.Pal.Filesystem;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>D3D12-backed <see cref="IModelLoader"/>. Loads each virtual path exactly
/// once through <see cref="D3D12GltfSceneLoader.Load"/>, builds a
/// <see cref="MultiMaterialAtlas"/> via <see cref="MultiMaterialAtlasBuilder.BuildFromGlb"/>
/// (per-material albedo, KHR-spec-gloss aware — required so Sketchfab vehicle exports
/// like the bundled the medium tank render with their authored camo textures instead of falling
/// through to a flat white fallback), and caches the resulting <see cref="D3D12Model"/>
/// by virtual path so repeated loads are free.
/// <para>
/// Disposal walks the cache and disposes every model — every GPU buffer + the per-model
/// atlas SRV/texture release back to the device. Hosts wire one loader per D3D12 client
/// instance via DI; the singleton's lifetime matches the host bundle's.
/// </para></summary>
internal sealed class D3D12ModelLoader : IModelLoader, IDisposable
{
    private const string GpuNamePrefix = "client.d3d12.model";

    private readonly D3D12RhiDevice _device;
    private readonly IVfs _vfs;
    private readonly ILogger<D3D12ModelLoader> _logger;
    private readonly Dictionary<string, D3D12Model> _cache = new(StringComparer.Ordinal);
    private int _instanceCounter;
    private bool _disposed;

    public D3D12ModelLoader(D3D12RhiDevice device, IVfs vfs, ILogger<D3D12ModelLoader> logger)
    {
        _device = Ensure.NotNull(device);
        _vfs = Ensure.NotNull(vfs);
        _logger = Ensure.NotNull(logger);
    }

    public IModel Load(string virtualPath)
    {
        Ensure.NotNullOrEmpty(virtualPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_cache.TryGetValue(virtualPath, out var cached))
        {
            return cached;
        }

        if (!_vfs.Exists(virtualPath))
        {
            _logger.LogWarning("Model not found at {Path}; rendering will be no-op.", virtualPath);
            _cache[virtualPath] = D3D12Model.Invalid;
            return D3D12Model.Invalid;
        }

        var realPath = _vfs.Realize(virtualPath);
        var instanceId = System.Threading.Interlocked.Increment(ref _instanceCounter);
        var namePrefix = $"{GpuNamePrefix}.{instanceId}";
        GltfSceneGpuAssets? assets = null;
        MultiMaterialAtlas? atlas = null;
        D3D12Model? model = null;
        try
        {
            var loadedAssets = D3D12GltfSceneLoader.Load(_device, realPath, namePrefix);
            assets = loadedAssets;
            var loadedAtlas = MultiMaterialAtlasBuilder.BuildFromGlb(
                _device,
                loadedAssets.GlbBytes,
                namePrefix);
            atlas = loadedAtlas;
            model = D3D12Model.From(loadedAssets, loadedAtlas);
            assets = null;
            atlas = null;
            _cache[virtualPath] = model;
            _logger.LogInformation(
                "Loaded model {Path}: bounds=[{Min} .. {Max}], {Primitives} primitives, {Materials} materials, {UniqueImages} unique textures.",
                virtualPath,
                model.BoundsMin,
                model.BoundsMax,
                loadedAssets.GpuScene.Primitives.Length,
                loadedAtlas.MaterialCount,
                loadedAtlas.UniqueImageCount);
            return model;
        }
        catch (Exception ex)
        {
            _cache.Remove(virtualPath);
            model?.Dispose();
            atlas?.Dispose();
            DisposeGpuScene(assets?.GpuScene);
            _logger.LogError(ex, "D3D12 glTF loader failed for {Path}; rendering will be no-op.", realPath);
            _cache[virtualPath] = D3D12Model.Invalid;
            return D3D12Model.Invalid;
        }
    }

    private static void DisposeGpuScene(GpuScene? scene)
    {
        if (scene is null)
        {
            return;
        }

        foreach (var primitive in scene.Primitives)
        {
            primitive.Ib.Dispose();
            primitive.Vb.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var (_, model) in _cache)
        {
            if (model.IsValid)
            {
                model.Dispose();
            }
        }

        _cache.Clear();
    }
}
