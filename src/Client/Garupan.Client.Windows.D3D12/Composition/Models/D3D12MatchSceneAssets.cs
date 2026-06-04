using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Garupan.Content;
using Garupan.Sim.Terrain;
using Opus.Engine.Pal.Filesystem;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Engine.Ui.Direct3D12;
using Opus.Foundation;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>
/// Owns the lifetime of one match scene's GPU assets — the tank model + its per-material atlas, the
/// fallback ground draw, the bundled terrain (mesh, atlas, heightfield), and the optional shot-VFX
/// sprites. Loads them in idempotent slices so <see cref="D3D12MatchSceneRenderer"/> can stream them
/// in over several frames behind a progress bar instead of blocking the first frame for seconds.
/// Split off the renderer so each side keeps one responsibility: this loads, holds, and disposes the
/// assets; the renderer composes + submits them. Properties are null until their loader has run —
/// the renderer touches them only after the preload (or the lazy <see cref="EnsureLoaded"/>) completes.
/// </summary>
internal sealed class D3D12MatchSceneAssets : IDisposable
{
    private const string TankModelPath = "res://tanks/vehicle_medium_b-rigged.glb";

    /// <summary>The PzGr 39 armour-piercing shell model fired downrange — a real projectile, not the
    /// procedural tracer cube. Optional: a build that does not bundle it falls back to the cube.</summary>
    private const string ShellModelPath = "res://shell/pzgr39.glb";

    /// <summary>Scales the engine's 100 m floor quad up so the fallback ground reaches past the
    /// camera far plane when no terrain is bundled. Flat tinted colour, so stretching it is free.</summary>
    private const float FloorHalfExtentScale = 6f;

    /// <summary>Flat ground tint for the fallback floor (which samples the atlas white slot).</summary>
    private static readonly Vector4 GroundTint = new(0.34f, 0.40f, 0.26f, 1f);

    private readonly D3D12RhiDevice _device;
    private readonly D3D12DrawSurface _drawSurface;
    private readonly IVfs _vfs;
    private readonly BattleMapCatalog _battleMaps;
    private readonly string _namePrefix;

    private bool _terrainLoadAttempted;
    private bool _propsLoadAttempted;
    private bool _shotVfxLoadAttempted;
    private BattleMapSpec? _map;
    private bool _disposed;

    public D3D12MatchSceneAssets(
        D3D12RhiDevice device,
        D3D12DrawSurface drawSurface,
        IVfs vfs,
        BattleMapCatalog battleMaps,
        string namePrefix)
    {
        _device = Ensure.NotNull(device);
        _drawSurface = Ensure.NotNull(drawSurface);
        _vfs = Ensure.NotNull(vfs);
        _battleMaps = Ensure.NotNull(battleMaps);
        _namePrefix = Ensure.NotNull(namePrefix);
    }

    /// <summary>Tank model + spliced floor / shell templates. Non-null after <see cref="LoadTank"/>.</summary>
    public GarageSceneAssets? Tank { get; private set; }

    /// <summary>Per-material tank atlas (camo + mips). Non-null after <see cref="LoadTank"/>.</summary>
    public MultiMaterialAtlas? TankAtlas { get; private set; }

    /// <summary>The fallback flat ground draw, used only when no terrain is bundled.</summary>
    public IReadOnlyList<SceneNodeDraw>? FloorDraws { get; private set; }

    /// <summary>Poses the tank's turret / gun / tracks. Non-null after <see cref="LoadTank"/>.</summary>
    public TankSceneArticulator? Articulator { get; private set; }

    /// <summary>The loaded terrain mesh, or null when no map is bundled (flat-floor fallback).</summary>
    public GltfSceneGpuAssets? TerrainAssets { get; private set; }

    /// <summary>The terrain's material atlas, or null without a map.</summary>
    public MultiMaterialAtlas? TerrainAtlas { get; private set; }

    /// <summary>The terrain heightfield used to seat tanks + rounds, or null without a map.</summary>
    public TerrainHeightField? Terrain { get; private set; }

    /// <summary>The map's destructible-prop scenery (renders poles, signs, bins client-side from the
    /// authoritative felled set), or null when the map ships no props file.</summary>
    public D3D12MatchPropScenery? Props { get; private set; }

    /// <summary>The optional shot-VFX sprite renderer, or null when its assets aren't bundled.</summary>
    public MatchShotVfxRenderer? ShotVfx { get; private set; }

    /// <summary>Loads everything in one go — the lazy fallback for callers (the GPU smoke tests) that
    /// drive the renderer directly rather than through the sliced preload. Idempotent.</summary>
    public void EnsureLoaded()
    {
        LoadTank();
        LoadTerrain();
        LoadProps();
        LoadShotVfx();
    }

    public void LoadTank()
    {
        if (Tank is not null)
        {
            return;
        }

        var realPath = _vfs.Realize(TankModelPath);
        var shellPath = _vfs.Exists(ShellModelPath) ? _vfs.Realize(ShellModelPath) : null;
        Tank = GarageSceneAssets.Load(_device, realPath, _namePrefix, shellPath);
        Articulator = new TankSceneArticulator(Tank.TankScene, Tank.TankTemplate);

        // Per-material atlas (camo + mips), NOT the bundle's single-texture atlas. The fallback
        // floor samples the white slot, tinted as ground.
        TankAtlas = MultiMaterialAtlasBuilder.BuildFromGlb(_device, File.ReadAllBytes(realPath), _namePrefix);
        FloorDraws = new[]
        {
            new SceneNodeDraw(
                Tank.StaticDraws[0].MeshIndex,
                Matrix4x4.CreateScale(FloorHalfExtentScale, 1f, FloorHalfExtentScale),
                GroundTint),
        };
    }

    /// <summary>Loads the first complete battle-map candidate: terrain mesh, material atlas, and
    /// matching heightfield. Latched + a silent no-op when none is bundled (flat-floor fallback).</summary>
    public void LoadTerrain()
    {
        if (_terrainLoadAttempted)
        {
            return;
        }

        _terrainLoadAttempted = true;
        var map = _battleMaps.ResolveFirstRenderable(
            fileName => _vfs.Exists(BattleMapVfsPaths.AssetPath(fileName)));
        if (map is null)
        {
            return;
        }

        _map = map;
        var terrainPath = _vfs.Realize(BattleMapVfsPaths.AssetPath(map.RenderModelFileName));
        var glbBytes = File.ReadAllBytes(terrainPath);
        TerrainAssets = D3D12GltfSceneLoader.Load(_device, terrainPath, $"{_namePrefix}.terrain");
        TerrainAtlas = BuildTerrainAtlas(map, terrainPath, glbBytes);
        Terrain = TerrainHeightField.Load(
            File.ReadAllBytes(_vfs.Realize(BattleMapVfsPaths.AssetPath(map.HeightFieldFileName))));
    }

    /// <summary>Loads the active map's destructible-prop layout and builds its scenery, once. Needs
    /// the tank loaded (for the null-material prop-box mesh slice) and the terrain resolved (for the
    /// map + heightfield), so it runs after both. A latched no-op when the map ships no props file.</summary>
    public void LoadProps()
    {
        if (_propsLoadAttempted)
        {
            return;
        }

        _propsLoadAttempted = true;
        if (Tank is null || _map?.PropsFileName is not { } propsFileName)
        {
            return;
        }

        var propsAssetPath = BattleMapVfsPaths.AssetPath(propsFileName);
        if (!_vfs.Exists(propsAssetPath))
        {
            return;
        }

        var layout = MapPropCsv.LoadFile(_vfs.Realize(propsAssetPath));
        Props = new D3D12MatchPropScenery(layout, Terrain, Tank.PropBoxMeshIndex);
    }

    /// <summary>Loads the optional shot-VFX sprite renderer once (null when its assets aren't
    /// bundled). Latched so a missing-asset build doesn't re-probe disk every frame.</summary>
    public void LoadShotVfx()
    {
        if (_shotVfxLoadAttempted)
        {
            return;
        }

        _shotVfxLoadAttempted = true;
        ShotVfx = MatchShotVfxRenderer.TryLoad(_device, _drawSurface, _vfs, $"{_namePrefix}.vfx");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        TankAtlas?.Dispose();
        TerrainAtlas?.Dispose();
        ShotVfx?.Dispose();
        DisposeTerrainPrimitives();
        Tank?.Dispose();
    }

    /// <summary>Selects the material atlas for the loaded map. When the map ships a sibling
    /// <c>{mapId}/textures/</c> directory of authored PBR sets the disk loader block-compresses
    /// (BC7/BC5) + binds them; otherwise the map's glb-embedded images are used. Keyed purely on
    /// directory existence, so a map opts into external textures by shipping the folder.</summary>
    private MultiMaterialAtlas BuildTerrainAtlas(BattleMapSpec map, string terrainPath, byte[] glbBytes)
    {
        var texturesRoot = Path.Combine(Path.GetDirectoryName(terrainPath)!, map.Id, "textures");
        return Directory.Exists(texturesRoot)
            ? ExternalMaterialAtlasBuilder.BuildFromDirectory(_device, glbBytes, texturesRoot, $"{_namePrefix}.terrain")
            : MultiMaterialAtlasBuilder.BuildFromGlb(_device, glbBytes, $"{_namePrefix}.terrain");
    }

    private void DisposeTerrainPrimitives()
    {
        if (TerrainAssets is null)
        {
            return;
        }

        foreach (var primitive in TerrainAssets.GpuScene.Primitives)
        {
            primitive.Vb.Dispose();
            primitive.Ib.Dispose();
        }
    }
}
