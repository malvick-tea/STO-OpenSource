using System;
using System.IO;
using System.Numerics;
using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Garupan.Client.Windows.Direct3D12.Composition.Models;
using Garupan.Client.Windows.Direct3D12.Tests.Fixtures;
using Garupan.Content;
using Garupan.Sim.Components;
using Garupan.Sim.Snapshot;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12;
using Opus.Engine.Ui.Direct3D12.Text;
using Xunit;

namespace Garupan.Client.Windows.Direct3D12.Tests.Composition.Models;

/// <summary>GPU smoke for <see cref="D3D12MatchSceneRenderer"/> — the V3b proof that the
/// <see cref="IMatchSceneRenderer"/> contract drives the engine's forward renderer (tank
/// template + procedural floor) through an offscreen viewport and composites into the UI
/// surface without exception. Mirrors <c>D3D12ModelRendererSmokeTests</c> but submits a
/// <see cref="NetworkMatchScenePlan"/> instead of a single model.</summary>
public sealed class D3D12MatchSceneRendererSmokeTests
{
    private const int FrameCount = 5;
    private const float AtlasPixelHeight = 24f;
    private const int AtlasSize = 512;
    private const string TankVirtualPath = "res://tanks/vehicle_medium_b-rigged.glb";
    private const string TankRelativePath = "content/tanks/vehicle_medium_b-rigged.glb";
    private const string ShellVirtualPath = "res://shell/pzgr39.glb";
    private const string ShellRelativePath = "content/shell/pzgr39.glb";
    private const string MuzzleVirtualPath = "res://vfx/kenney-particle-pack/muzzle_01.png";
    private const string MuzzleRelativePath = "content/vfx/kenney-particle-pack/muzzle_01.png";
    private const string SmokeVirtualPath = "res://vfx/kenney-particle-pack/smoke_04.png";
    private const string SmokeRelativePath = "content/vfx/kenney-particle-pack/smoke_04.png";
    private const string DirtVirtualPath = "res://vfx/kenney-particle-pack/dirt_03.png";
    private const string DirtRelativePath = "content/vfx/kenney-particle-pack/dirt_03.png";
    private const string BattleMapCatalogRelativePath = "content/maps/catalog.csv";
    private const string MapGlbVirtualPath = "res://maps/japan.glb";
    private const string MapGlbRelativePath = "content/maps/japan.glb";
    private const string MapHeightfieldVirtualPath = "res://maps/japan.heightfield";
    private const string MapHeightfieldRelativePath = "content/maps/japan.heightfield";
    private const string MapPropsVirtualPath = "res://maps/japan-props.csv";
    private const string MapPropsRelativePath = "content/maps/japan-props.csv";
    private const string MapObstaclesVirtualPath = "res://maps/japan-obstacles.csv";
    private const string MapObstaclesRelativePath = "content/maps/japan-obstacles.csv";

    private static readonly Color BackgroundColor = new(15, 20, 28, 255);
    private static readonly string[] AtlasCorpus = new[] { "MATCH" };

    [SkippableFact]
    public void Renders_a_match_plan_with_floor_across_multiple_frames_without_exception()
    {
        using var harness = MatchSceneHarness.TryCreate();
        Skip.If(harness is null, "D3D12 device or tank asset unavailable.");

        var plan = BuildPlan();
        var drive = () => harness!.DriveFrames(plan, FrameCount);
        drive.Should().NotThrow("the match scene renderer must drive the floor + tanks through the GPU");
    }

    [SkippableFact]
    public void Renders_an_empty_plan_as_floor_only_without_exception()
    {
        using var harness = MatchSceneHarness.TryCreate();
        Skip.If(harness is null, "D3D12 device or tank asset unavailable.");

        var emptyPlan = new NetworkMatchScenePlan(BuildCamera(), Array.Empty<TankPlacement>());
        var drive = () => harness!.DriveFrames(emptyPlan, FrameCount);
        drive.Should().NotThrow("a tankless plan must render the floor alone");
    }

    [SkippableFact]
    public void Sliced_preload_then_render_drives_without_exception()
    {
        using var harness = MatchSceneHarness.TryCreate();
        Skip.If(harness is null, "D3D12 device or tank asset unavailable.");

        var plan = BuildPlan();
        var drive = () =>
        {
            harness!.Preload();
            harness.DriveFrames(plan, FrameCount);
        };
        drive.Should().NotThrow("the sliced preload must load the same assets the lazy Render path does");
    }

    [SkippableFact]
    public void Renders_the_city_props_and_a_felled_set_across_frames_without_exception()
    {
        using var harness = MatchSceneHarness.TryCreate(mapBattleMap: true);
        Skip.If(harness is null, "D3D12 device, tank asset, or japan map unavailable.");

        // A standing city (thousands of poles/signs/bins) plus one prop mid-topple, one fully fallen,
        // and one shattered — the authoritative felled set the snapshot carries.
        var plan = BuildPlan() with
        {
            DestroyedProps = new[]
            {
                new PropSnapshot(PropId: 0, State: PropState.Toppling, FallYawRadians: 0.5f, ToppleSeconds: 0.4f),
                new PropSnapshot(PropId: 1, State: PropState.Fallen, FallYawRadians: 2f, ToppleSeconds: 0.8f),
                new PropSnapshot(PropId: 2, State: PropState.Broken, FallYawRadians: 0f, ToppleSeconds: 0f),
            },
        };
        var drive = () =>
        {
            harness!.Preload();
            harness.DriveFrames(plan, FrameCount);
        };
        drive.Should().NotThrow("the map's destructible props + a felled override must render through the GPU");
    }

    private static NetworkMatchScenePlan BuildPlan() => new(
        BuildCamera(),
        new[]
        {
            new TankPlacement(new Vector3(0f, 0f, 0f), 0f, IsSelf: true, KnockedOut: false, EntityId: 1, TurretYawRadians: MathF.PI / 4f),
            new TankPlacement(new Vector3(12f, 0f, 6f), MathF.PI, IsSelf: false, KnockedOut: false, EntityId: 2),
            new TankPlacement(new Vector3(-8f, 0f, 4f), MathF.PI / 2f, IsSelf: false, KnockedOut: true, EntityId: 3),
        })
    {
        Projectiles = new[]
        {
            new ProjectilePlacement(new Vector3(4f, 1.5f, 3f), new Vector3(0f, 0f, 14f), 10, new Vector3(0f, 1.5f, 0f)),
            new ProjectilePlacement(new Vector3(-3f, 1.5f, 8f), new Vector3(-10f, 0f, 0f), 11, new Vector3(1f, 1.5f, 1f)),
        },
    };

    private static CameraView3D BuildCamera() =>
        CameraView3D.LookAt(new Vector3(0f, 9f, -18f), new Vector3(0f, 2f, 0f), fovY: 50f);

    /// <summary>Owns the D3D12 host artefacts the smoke needs; null when the device or the
    /// bundled tank asset is missing (the test then skips). Disposes in reverse order.</summary>
    private sealed class MatchSceneHarness : IDisposable
    {
        private readonly D3D12WindowSession _session;
        private readonly D3D12FontAtlas _atlas;
        private readonly D3D12DrawSurface _surface;
        private readonly D3D12UiFrameLoop _loop;
        private readonly D3D12MatchSceneRenderer _renderer;

        private MatchSceneHarness(D3D12WindowSession session, D3D12FontAtlas atlas, D3D12DrawSurface surface, D3D12UiFrameLoop loop, D3D12MatchSceneRenderer renderer)
        {
            _session = session;
            _atlas = atlas;
            _surface = surface;
            _loop = loop;
            _renderer = renderer;
        }

        public static MatchSceneHarness? TryCreate(bool mapBattleMap = false)
        {
            var session = D3D12HostTestFixture.TryAcquire();
            if (session is null)
            {
                return null;
            }

            var assetPath = Path.Combine(AppContext.BaseDirectory, TankRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(assetPath))
            {
                return null;
            }

            // The prop smoke needs a map with destructible props bundled; skip when it isn't.
            if (mapBattleMap
                && !File.Exists(Path.Combine(AppContext.BaseDirectory, MapGlbRelativePath.Replace('/', Path.DirectorySeparatorChar))))
            {
                return null;
            }

            var atlas = D3D12FontAtlas.BuildAndUpload(session.Device, AtlasCorpus, AtlasPixelHeight, AtlasSize);
            var surface = D3D12DrawSurface.Create(session.Device, atlas, session.Compiler, session.SwapChain.Format);
            var loop = new D3D12UiFrameLoop(session);
            var vfs = new FakeVfs().Map(TankVirtualPath, assetPath);
            var shellPath = Path.Combine(AppContext.BaseDirectory, ShellRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(shellPath))
            {
                vfs.Map(ShellVirtualPath, shellPath);
            }

            MapIfExists(vfs, MuzzleVirtualPath, MuzzleRelativePath);
            MapIfExists(vfs, SmokeVirtualPath, SmokeRelativePath);
            MapIfExists(vfs, DirtVirtualPath, DirtRelativePath);
            if (mapBattleMap)
            {
                MapIfExists(vfs, MapGlbVirtualPath, MapGlbRelativePath);
                MapIfExists(vfs, MapHeightfieldVirtualPath, MapHeightfieldRelativePath);
                MapIfExists(vfs, MapPropsVirtualPath, MapPropsRelativePath);
                MapIfExists(vfs, MapObstaclesVirtualPath, MapObstaclesRelativePath);
            }

            var catalogPath = Path.Combine(
                AppContext.BaseDirectory,
                BattleMapCatalogRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var maps = BattleMapCsv.LoadFile(catalogPath);
            var renderer = new D3D12MatchSceneRenderer(session.Device, session.Compiler, session.SwapChain, surface, vfs, maps);
            return new MatchSceneHarness(session, atlas, surface, loop, renderer);
        }

        private static void MapIfExists(FakeVfs vfs, string virtualPath, string relativePath)
        {
            var path = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                vfs.Map(virtualPath, path);
            }
        }

        /// <summary>Runs the sliced preload to completion — the runtime path the screen drives
        /// one step per frame — so the smoke proves it loads the same assets the lazy path does.</summary>
        public void Preload()
        {
            var preload = _renderer.BeginPreload();
            while (preload.Advance())
            {
            }
        }

        public void DriveFrames(NetworkMatchScenePlan plan, int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                var frame = _loop.BeginFrame();
                _surface.BeginFrame(frame.CommandList, frame.RenderTargetView, frame.BackBufferSlot, frame.ViewportWidth, frame.ViewportHeight);
                _surface.Clear(BackgroundColor);
                _renderer.Render(plan);
                _surface.EndFrame();
                _loop.EndFrame();
                _session.Window.PollEvents();
            }
        }

        public void Dispose()
        {
            _renderer.Dispose();
            _loop.Dispose();
            _surface.Dispose();
            _atlas.Dispose();
        }
    }
}
