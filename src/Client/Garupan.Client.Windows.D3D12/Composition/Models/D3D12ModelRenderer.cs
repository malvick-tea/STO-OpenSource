using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Engine.Renderer.Direct3D12;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12;
using Opus.Foundation;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>D3D12-backed <see cref="IModelRenderer"/>. Implements the screen-facing
/// 3D contract by composing the runtime scene renderer (
/// <see cref="D3D12ForwardSceneRenderer"/>) on top of an offscreen
/// <see cref="D3D12SceneViewport"/> and emitting the result as a composite quad into
/// the active <see cref="D3D12DrawSurface"/> frame.
/// <para>
/// Frame ordering: <see cref="BeginScene"/> captures the camera and starts a fresh draw
/// list; <see cref="DrawModel"/> / <see cref="DrawModelEx"/> append per-model world
/// transforms; <see cref="EndScene"/> drives the offscreen forward + tonemap pass set
/// and writes the alpha-aware composite quad into the surface. Sky pixels in the
/// offscreen target rest at alpha=0 so the chrome drawn before the scene reads through;
/// mesh pixels write opaque alpha and cover the chrome where they project.
/// </para>
/// <para>
/// Viewport / forward renderer are lazily created (and re-created on draw-surface
/// resize) so the scene matches the host's current resolution without paying the GPU
/// resource cost up front. Disposes both on shutdown.
/// </para>
/// <para>
/// The device-free maths — camera projection, tint conversion, node-graph flattening —
/// lives in <see cref="ModelScenePlan"/> so it stays unit-testable without a GPU.
/// </para></summary>
internal sealed class D3D12ModelRenderer : IModelRenderer, IDisposable
{
    private const string ViewportNamePrefix = "client.d3d12.garage";

    private readonly D3D12RhiDevice _device;
    private readonly D3D12ShaderCompiler _compiler;
    private readonly D3D12SwapChain _swapChain;
    private readonly D3D12DrawSurface _drawSurface;
    private readonly List<PendingDraw> _draws = new();

    private D3D12SceneViewport? _viewport;
    private D3D12ForwardSceneRenderer? _forward;
    private int _viewportWidth;
    private int _viewportHeight;

    private CameraView3D _camera;
    private bool _inScene;
    private bool _disposed;

    public D3D12ModelRenderer(
        D3D12RhiDevice device,
        D3D12ShaderCompiler compiler,
        D3D12SwapChain swapChain,
        D3D12DrawSurface drawSurface)
    {
        _device = Ensure.NotNull(device);
        _compiler = Ensure.NotNull(compiler);
        _swapChain = Ensure.NotNull(swapChain);
        _drawSurface = Ensure.NotNull(drawSurface);
    }

    public void BeginScene(in CameraView3D camera)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _camera = camera;
        _draws.Clear();
        _inScene = true;
    }

    public void DrawModel(IModel model, Vector3 position, float scale, Color tint)
    {
        if (!_inScene || model is not D3D12Model m || !m.IsValid)
        {
            return;
        }

        var world = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(position);
        _draws.Add(new PendingDraw(m, world, ModelScenePlan.ToTintFactor(tint)));
    }

    public void DrawModelEx(
        IModel model,
        Vector3 position,
        Vector3 rotationAxis,
        float rotationDegrees,
        Vector3 scale,
        Color tint)
    {
        if (!_inScene || model is not D3D12Model m || !m.IsValid)
        {
            return;
        }

        var rotation = Matrix4x4.CreateFromAxisAngle(rotationAxis, rotationDegrees * (MathF.PI / 180f));
        var world = Matrix4x4.CreateScale(scale) * rotation * Matrix4x4.CreateTranslation(position);
        _draws.Add(new PendingDraw(m, world, ModelScenePlan.ToTintFactor(tint)));
    }

    public void EndScene()
    {
        if (!_inScene)
        {
            return;
        }

        _inScene = false;
        if (_draws.Count == 0)
        {
            return;
        }

        var width = _drawSurface.Width;
        var height = _drawSurface.Height;
        if (width <= 0 || height <= 0)
        {
            _draws.Clear();
            return;
        }

        EnsureViewport(width, height);
        var nodeDraws = ModelScenePlan.FlattenNodeDraws(BuildPlacements());
        var primary = _draws[0].Model;
        var cameras = ModelScenePlan.BuildCameras(_camera, width, height);
        var target = _viewport!.CreateRenderTargetDescriptor();

        _forward!.Render(
            _viewport.Renderer,
            primary.Assets!.GpuScene,
            nodeDraws,
            primary.Atlas!,
            cameras,
            SceneRenderDefaults.Lighting,
            SceneRenderDefaults.PostFx,
            target);

        unsafe
        {
            _drawSurface.DrawTexturedRect(
                _viewport.Target.SrvTable,
                _viewport.Target.SrvHeap,
                x: 0,
                y: 0,
                w: width,
                h: height);
        }

        _draws.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _forward?.Dispose();
        _viewport?.Dispose();
    }

    private void EnsureViewport(int width, int height)
    {
        if (_viewport is not null && _forward is not null && _viewportWidth == width && _viewportHeight == height)
        {
            return;
        }

        _forward?.Dispose();
        _viewport?.Dispose();
        _viewport = new D3D12SceneViewport(_device, _swapChain, width, height, ViewportNamePrefix);
        _forward = new D3D12ForwardSceneRenderer(
            _device,
            _compiler,
            _viewport.Target.Format,
            width,
            height,
            $"{ViewportNamePrefix}.forward");
        _viewportWidth = width;
        _viewportHeight = height;
    }

    private ModelPlacement[] BuildPlacements()
    {
        var placements = new ModelPlacement[_draws.Count];
        for (var i = 0; i < _draws.Count; i++)
        {
            var draw = _draws[i];
            placements[i] = new ModelPlacement(draw.Model.Assets!.NodeDraws, draw.World, draw.Tint);
        }

        return placements;
    }

    private readonly record struct PendingDraw(D3D12Model Model, Matrix4x4 World, Vector4 Tint);
}
