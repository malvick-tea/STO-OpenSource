using System;
using System.IO;
using System.Numerics;
using FluentAssertions;
using Garupan.Client.Windows.Direct3D12.Composition.Models;
using Garupan.Client.Windows.Direct3D12.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12;
using Opus.Engine.Ui.Direct3D12.Text;
using Xunit;

namespace Garupan.Client.Windows.Direct3D12.Tests.Composition.Models;

/// <summary>GPU smoke for <see cref="D3D12ModelRenderer"/> — the Phase D2b end-to-end
/// proof that the screen-facing <see cref="IModelRenderer"/> contract drives the
/// runtime scene renderer through an offscreen viewport and composites the result
/// into the UI surface without exception. Mirrors the shape of
/// <c>D3D12DrawSurfaceIntegrationTests</c> (chrome batch + multi-frame drive) but with
/// the model renderer's <c>BeginScene</c>/<c>DrawModel</c>/<c>EndScene</c> bracket
/// interleaved.</summary>
public sealed class D3D12ModelRendererSmokeTests
{
    private const int FrameCount = 5;
    private const float AtlasPixelHeight = 24f;
    private const int AtlasSize = 512;
    private const string TankVirtualPath = "res://tanks/vehicle_medium_b-rigged.glb";
    private const string TankRelativePath = "content/tanks/vehicle_medium_b-rigged.glb";

    private static readonly Color BackgroundColor = new(15, 20, 28, 255);
    private static readonly Color ChromeColor = new(220, 180, 60, 255);
    private static readonly Color TextColor = new(240, 240, 240, 255);
    private static readonly string[] AtlasCorpus = new[] { "GARAGE" };

    [SkippableFact]
    public void Drives_chrome_plus_offscreen_scene_plus_overlay_across_multiple_frames()
    {
        var session = D3D12HostTestFixture.TryAcquire();
        D3D12HostTestFixture.SkipIfUnavailable(session);

        var assetPath = Path.Combine(AppContext.BaseDirectory, TankRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Skip.IfNot(File.Exists(assetPath), $"Tank asset missing: {assetPath}");

        using var atlas = D3D12FontAtlas.BuildAndUpload(session!.Device, AtlasCorpus, AtlasPixelHeight, AtlasSize);
        using var surface = D3D12DrawSurface.Create(session.Device, atlas, session.Compiler, session.SwapChain.Format);
        using var loop = new D3D12UiFrameLoop(session);

        var vfs = new FakeVfs().Map(TankVirtualPath, assetPath);
        using var loader = new D3D12ModelLoader(session.Device, vfs, NullLogger<D3D12ModelLoader>.Instance);
        using var renderer = new D3D12ModelRenderer(session.Device, session.Compiler, session.SwapChain, surface);

        var model = loader.Load(TankVirtualPath);
        model.IsValid.Should().BeTrue("the bundled tank glb is a valid asset");

        DriveFrames(session, surface, loop, renderer, model);

        surface.Width.Should().Be(session.SwapChain.Width);
        surface.Height.Should().Be(session.SwapChain.Height);
    }

    [SkippableFact]
    public void Invalid_model_renders_as_no_op_without_composite_or_exception()
    {
        var session = D3D12HostTestFixture.TryAcquire();
        D3D12HostTestFixture.SkipIfUnavailable(session);

        using var atlas = D3D12FontAtlas.BuildAndUpload(session!.Device, AtlasCorpus, AtlasPixelHeight, AtlasSize);
        using var surface = D3D12DrawSurface.Create(session.Device, atlas, session.Compiler, session.SwapChain.Format);
        using var loop = new D3D12UiFrameLoop(session);
        using var renderer = new D3D12ModelRenderer(session.Device, session.Compiler, session.SwapChain, surface);

        var frame = loop.BeginFrame();
        surface.BeginFrame(frame.CommandList, frame.RenderTargetView, frame.BackBufferSlot, frame.ViewportWidth, frame.ViewportHeight);
        surface.Clear(BackgroundColor);

        renderer.BeginScene(BuildCamera(centre: Vector3.Zero, distance: 6f));
        renderer.DrawModel(D3D12Model.Invalid, Vector3.Zero, scale: 1f, ChromeColor);
        var endScene = () => renderer.EndScene();
        endScene.Should().NotThrow("the invalid placeholder must drop every draw without touching the scene path");

        surface.EndFrame();
        loop.EndFrame();
        session.Window.PollEvents();
    }

    private static void DriveFrames(D3D12WindowSession session, D3D12DrawSurface surface, D3D12UiFrameLoop loop, D3D12ModelRenderer renderer, IModel model)
    {
        var centre = (model.BoundsMin + model.BoundsMax) * 0.5f;
        var size = model.BoundsMax - model.BoundsMin;
        var distance = MathF.Max(MathF.Max(size.X, size.Y), size.Z) * 2.4f;

        for (var i = 0; i < FrameCount; i++)
        {
            var angle = (i / (float)FrameCount) * MathF.Tau;
            var cameraPos = centre + new Vector3(MathF.Cos(angle) * distance, distance * 0.55f, MathF.Sin(angle) * distance);
            var camera = CameraView3D.LookAt(cameraPos, centre, fovY: 38f);

            var frame = loop.BeginFrame();
            surface.BeginFrame(frame.CommandList, frame.RenderTargetView, frame.BackBufferSlot, frame.ViewportWidth, frame.ViewportHeight);
            surface.Clear(BackgroundColor);
            surface.FillRect(0, 0, surface.Width, 56, ChromeColor);

            renderer.BeginScene(camera);
            renderer.DrawModel(model, Vector3.Zero, scale: 1f, Color.White);
            renderer.EndScene();

            surface.DrawText("GARAGE", 24, 14, fontSize: 22, TextColor);
            surface.EndFrame();
            loop.EndFrame();
            session.Window.PollEvents();
        }
    }

    private static CameraView3D BuildCamera(Vector3 centre, float distance)
    {
        var position = centre + new Vector3(distance, distance * 0.55f, 0f);
        return CameraView3D.LookAt(position, centre, fovY: 38f);
    }
}
