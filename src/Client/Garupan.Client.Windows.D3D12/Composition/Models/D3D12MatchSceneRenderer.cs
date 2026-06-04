using System;
using System.Collections.Generic;
using System.Numerics;
using Garupan.Client.Ui.Match.Network;
using Garupan.Content;
using Garupan.Sim.Terrain;
using Opus.Engine.Pal.Filesystem;
using Opus.Engine.Renderer;
using Opus.Engine.Renderer.Direct3D12;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12;
using Opus.Foundation;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>
/// D3D12-backed <see cref="IMatchSceneRenderer"/>. Renders the network match's 3D scene —
/// the terrain (or a flat fallback ground) plus every tank — by composing the engine's
/// runtime forward renderer inside one offscreen <see cref="D3D12SceneViewport"/> and
/// compositing the results into the active <see cref="D3D12DrawSurface"/> frame.
/// </summary>
/// <remarks>
/// One scene pass per UI frame: terrain/buildings and tanks/props/shells are separate material
/// layers in the same depth buffer, while keeping their own atlases. The pass clears
/// sky-transparent so the screen's 2D <c>NetworkMatchSkyBackdrop</c> reads through above the
/// horizon; all opaque layers share depth, so buildings, props, tanks, and shells occlude each
/// other without a merged atlas. With no map bundled it falls back to a flat tinted floor at Y=0,
/// so asset-light builds still render.
/// Asset lifetime (loading + holding + disposing the tank, atlases, terrain, shell templates) lives
/// in <see cref="D3D12MatchSceneAssets"/>, sliced through <see cref="BeginPreload"/> so the first
/// frame never blocks; plan → world-matrix maths in <see cref="MatchSceneInstances"/>, shot sprites
/// in <see cref="MatchShotVfxRenderer"/>. This class owns only the viewport + submission order.
/// </remarks>
internal sealed class D3D12MatchSceneRenderer : IMatchSceneRenderer, IDisposable
{
    private const string NamePrefix = "client.d3d12.match";

    /// <summary>Readable steel tint for the display-only shell mesh. Physical ballistics are
    /// unaffected; the geometry-only GLB shares the scene atlas.</summary>
    private static readonly Vector4 ShellTint = new(0.92f, 0.86f, 0.68f, 1f);

    private readonly D3D12RhiDevice _device;
    private readonly D3D12ShaderCompiler _compiler;
    private readonly D3D12SwapChain _swapChain;
    private readonly D3D12DrawSurface _drawSurface;
    private readonly TankAudioTracker? _tankAudio;
    private readonly D3D12MatchSceneAssets _sceneAssets;
    private readonly TankMotionTracker _tankMotion = new();

    private D3D12SceneViewport? _viewport;
    private D3D12ForwardSceneRenderer? _forward;

    private int _viewportWidth;
    private int _viewportHeight;
    private bool _disposed;

    public D3D12MatchSceneRenderer(
        D3D12RhiDevice device,
        D3D12ShaderCompiler compiler,
        D3D12SwapChain swapChain,
        D3D12DrawSurface drawSurface,
        IVfs vfs,
        BattleMapCatalog battleMaps,
        TankAudioTracker? tankAudio = null)
    {
        _device = Ensure.NotNull(device);
        _compiler = Ensure.NotNull(compiler);
        _swapChain = Ensure.NotNull(swapChain);
        _drawSurface = Ensure.NotNull(drawSurface);
        _tankAudio = tankAudio;
        _sceneAssets = new D3D12MatchSceneAssets(device, drawSurface, vfs, battleMaps, NamePrefix);
    }

    public void Render(NetworkMatchScenePlan plan)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Ensure.NotNull(plan);
        _tankAudio?.Resolve(plan);

        var width = _drawSurface.Width;
        var height = _drawSurface.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _sceneAssets.EnsureLoaded();
        EnsureViewport(width, height);

        var frameSurface = SurfaceFor(plan);
        var cameras = ModelScenePlan.BuildCameras(LiftCamera(plan.Camera, frameSurface), width, height);
        RenderViewport(_viewport!, _forward!, BuildRenderLayers(plan, frameSurface), cameras, width, height);
        _sceneAssets.ShotVfx?.Render(plan, cameras, width, height, _sceneAssets.Terrain);
    }

    /// <summary>Slices the heavy GPU loads into named steps the screen runs one-per-frame behind a
    /// progress bar. The same idempotent step methods back the lazy <see cref="Render"/> path, so a
    /// preloaded renderer no-ops there. The viewport step uses the current surface size; a later
    /// resize still rebuilds it through <see cref="EnsureViewport"/>.</summary>
    public IMatchScenePreload BeginPreload()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new MatchScenePreload(new[]
        {
            new MatchPreloadStep("Loading tank", _sceneAssets.LoadTank),
            new MatchPreloadStep("Loading battlefield", _sceneAssets.LoadTerrain),
            new MatchPreloadStep("Loading street props", _sceneAssets.LoadProps),
            new MatchPreloadStep("Loading effects", _sceneAssets.LoadShotVfx),
            new MatchPreloadStep("Preparing renderer", PrepareViewport),
        });
    }

    public void EndMatch()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _tankAudio?.Stop();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _tankAudio?.Stop();
        _disposed = true;
        _forward?.Dispose();
        _viewport?.Dispose();
        _sceneAssets.Dispose();
    }

    /// <summary>Runs one layered offscreen scene pass into <paramref name="viewport"/> and
    /// composites the result over the active UI surface.</summary>
    private void RenderViewport(
        D3D12SceneViewport viewport,
        D3D12ForwardSceneRenderer forward,
        IReadOnlyList<ForwardSceneRenderLayer> layers,
        FrameCameraSet cameras,
        int width,
        int height)
    {
        forward.Render(
            viewport.Renderer, layers, cameras,
            SceneRenderDefaults.MatchLighting, SceneRenderDefaults.PostFx,
            viewport.CreateRenderTargetDescriptor());

        unsafe
        {
            _drawSurface.DrawTexturedRect(
                viewport.Target.SrvTable, viewport.Target.SrvHeap, x: 0, y: 0, w: width, h: height);
        }
    }

    private IReadOnlyList<ForwardSceneRenderLayer> BuildRenderLayers(
        NetworkMatchScenePlan plan,
        IHeightSurface? frameSurface)
    {
        var tank = _sceneAssets.Tank!;
        var tankLayer = new ForwardSceneRenderLayer(
            tank.GpuScene,
            BuildNodeDraws(plan, frameSurface),
            _sceneAssets.TankAtlas!,
            tank.MeshLocalBounds);

        if (_sceneAssets.TerrainAssets is not { } terrain)
        {
            return new[] { tankLayer };
        }

        return new[]
        {
            new ForwardSceneRenderLayer(
                terrain.GpuScene,
                terrain.NodeDraws,
                _sceneAssets.TerrainAtlas!,
                terrain.MeshLocalBounds),
            tankLayer,
        };
    }

    /// <summary>Maps the tank instances + in-flight shells + destructible props onto the draw list,
    /// each lifted onto the terrain surface. The always-present floor is included only when no terrain
    /// is bundled (the terrain replaces it). Props share the vehicle layer so a tank correctly
    /// occludes - and is occluded by - a pole. An empty, prop-free match renders the floor alone.</summary>
    private IReadOnlyList<SceneNodeDraw> BuildNodeDraws(NetworkMatchScenePlan plan, IHeightSurface? frameSurface)
    {
        var instances = MatchSceneInstances.From(plan, frameSurface);
        var projectiles = MatchSceneInstances.ProjectileWorlds(plan);
        var floor = _sceneAssets.Terrain is null ? _sceneAssets.FloorDraws! : Array.Empty<SceneNodeDraw>();
        var props = _sceneAssets.Props;
        if (instances.Count == 0 && projectiles.Count == 0 && props is null)
        {
            return floor;
        }

        var tank = _sceneAssets.Tank!;
        var shellDrawsPerRound = tank.ShellTemplate?.Count ?? 1;
        var draws = new List<SceneNodeDraw>(
            floor.Count + (props?.StandingDrawCount ?? 0)
            + (instances.Count * tank.TankTemplate.Count) + (projectiles.Count * shellDrawsPerRound));
        draws.AddRange(floor);
        props?.AppendDraws(draws, plan.DestroyedProps);
        for (var i = 0; i < instances.Count; i++)
        {
            var motion = _tankMotion.Resolve(plan.Tanks[i], plan.SnapshotTick);

            // instances[i].World is already conformed to the terrain (height + slope tilt) when a
            // map is loaded, so no separate height lift here — the articulator poses turret, gun,
            // and tracks on top of the seated, tilted hull.
            draws.AddRange(_sceneAssets.Articulator!.BuildDraws(plan.Tanks[i], instances[i].World, instances[i].Tint, motion));
        }

        for (var i = 0; i < projectiles.Count; i++)
        {
            AppendShell(draws, LiftWorld(projectiles[i], projectiles[i].Translation));
        }

        return draws;
    }

    /// <summary>Appends one in-flight round: the spliced PzGr 39 shell template tinted readable
    /// steel, or — when no shell asset was bundled — the engine's procedural projectile cube.</summary>
    private void AppendShell(List<SceneNodeDraw> draws, in Matrix4x4 world)
    {
        var tank = _sceneAssets.Tank!;
        var template = tank.ShellTemplate;
        if (template is null)
        {
            draws.Add(new SceneNodeDraw(tank.ProjectileMeshIndex, world, ShellTint));
            return;
        }

        for (var i = 0; i < template.Count; i++)
        {
            var draw = template[i];
            draws.Add(draw with { World = draw.World * world, TintFactor = draw.TintFactor * ShellTint });
        }
    }

    /// <summary>Lifts an in-flight round onto the terrain: shifts its world up by the sampled
    /// surface height at its horizontal position so its visual arc rides above the relief, not the
    /// Y=0 plane. Hulls instead use the full terrain conform in <see cref="MatchSceneInstances"/>
    /// (height + slope tilt). Identity when no terrain is loaded.</summary>
    private Matrix4x4 LiftWorld(in Matrix4x4 world, in Vector3 groundPosition)
    {
        if (_sceneAssets.Terrain is not { } terrain)
        {
            return world;
        }

        return world * Matrix4x4.CreateTranslation(0f, terrain.HeightAt(groundPosition.X, groundPosition.Z), 0f);
    }

    /// <summary>Lifts the chase camera by the terrain height under its aim point, so it keeps a
    /// constant height above the tank rather than dipping below a hill. Identity without terrain.</summary>
    private CameraView3D LiftCamera(CameraView3D camera, IHeightSurface? frameSurface)
    {
        if (frameSurface is null)
        {
            return camera;
        }

        var lift = Vector3.UnitY * frameSurface.HeightAt(camera.Target.X, camera.Target.Z);
        return camera with { Position = camera.Position + lift, Target = camera.Target + lift };
    }

    private IHeightSurface? SurfaceFor(NetworkMatchScenePlan plan) =>
        _sceneAssets.Props?.HeightSurfaceFor(plan.DestroyedProps) ?? _sceneAssets.Terrain;

    /// <summary>Preload step: build the scene viewports + forward renderers (the pipeline-state
    /// compile) at the current surface size, so the first real <see cref="Render"/> doesn't stall.
    /// No-op on a zero-sized surface; a later resize rebuilds through <see cref="EnsureViewport"/>.</summary>
    private void PrepareViewport()
    {
        var width = _drawSurface.Width;
        var height = _drawSurface.Height;
        if (width > 0 && height > 0)
        {
            EnsureViewport(width, height);
        }
    }

    private void EnsureViewport(int width, int height)
    {
        if (_viewport is not null && _forward is not null && _viewportWidth == width && _viewportHeight == height)
        {
            return;
        }

        _forward?.Dispose();
        _viewport?.Dispose();

        _viewport = new D3D12SceneViewport(_device, _swapChain, width, height, NamePrefix);
        _forward = NewForward(_viewport, $"{NamePrefix}.forward", width, height);

        _viewportWidth = width;
        _viewportHeight = height;
    }

    private D3D12ForwardSceneRenderer NewForward(D3D12SceneViewport viewport, string name, int width, int height) =>
        new(_device, _compiler, viewport.Target.Format, width, height, name);
}
