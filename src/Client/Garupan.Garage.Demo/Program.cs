using System;
using System.IO;
using System.Numerics;
using Garupan.Content;
using Opus.Content;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Pal.Windows.Time;
using Opus.Engine.Renderer.Direct3D12;
using Opus.Engine.Renderer.Direct3D12.Scene;

namespace Garupan.Garage.Demo;

/// <summary>
/// Standalone Garage demo entry point. Owns the live D3D12 frame loop: brings up an SDL3
/// window + D3D12 device, loads the legacy the medium tank, constructs the canonical
/// <see cref="GarageSceneController"/>, and pumps a player-controlled tank vs. three AI
/// opponents until the window closes. Input wiring lives in <see cref="DemoInputBindings"/>,
/// outcome/restart logic lives in <see cref="MatchLifecycle"/>, and Sim-to-world pose
/// math lives in <see cref="SimToWorldMapper"/>.
/// </summary>
public static class Program
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;
    private const string WindowTitle = "STO Garage Demo";
    private const string TankAssetRelativePath = "content/tanks/vehicle_medium_a.glb";
    private const string SchoolPaletteRelativePath = "data/school-palette.csv";
    private const string MatchCompositionRelativePath = "data/garage-demo-match.csv";
    private const string LightingPresetRelativePath = "data/garage-lighting.csv";
    private const string ShellVisualsRelativePath = "data/shell-visuals.csv";

    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("STO garage demo requires Windows (D3D12).");
            return 2;
        }

        var assetPath = ResolveDataPath(TankAssetRelativePath);
        if (!File.Exists(assetPath))
        {
            Console.Error.WriteLine($"the medium tank asset missing at {assetPath}. Rebuild to copy content/tanks/vehicle_medium_a.glb next to the exe.");
            return 3;
        }

        SchoolPalette schoolPalette;
        MatchComposition matchComposition;
        LightingPreset lightingPreset;
        ShellVisualCatalog shellVisuals;
        try
        {
            schoolPalette = SchoolPaletteCsv.LoadFile(ResolveDataPath(SchoolPaletteRelativePath));
            matchComposition = MatchCompositionCsv.LoadFile(ResolveDataPath(MatchCompositionRelativePath));
            lightingPreset = LightingPresetCsv.LoadFile(ResolveDataPath(LightingPresetRelativePath));
            shellVisuals = ShellVisualCsv.LoadFile(ResolveDataPath(ShellVisualsRelativePath));
            var validation = CatalogValidator.Validate(schoolPalette);
            if (!validation.Ok)
            {
                Console.Error.WriteLine($"Catalog validation failed: {string.Join("; ", validation.Errors)}");
                return 6;
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or ArgumentException)
        {
            Console.Error.WriteLine($"Failed to load canon data: {ex.Message}");
            return 5;
        }

        using var session = D3D12WindowSession.TryOpen(
            D3D12WindowSessionOptions.Windowed(WindowTitle, WindowWidth, WindowHeight));
        if (session is null)
        {
            Console.Error.WriteLine("Failed to bring up SDL window or D3D12 device on this host.");
            return 4;
        }

        using var renderer = new D3D12Renderer(session.Device, session.SwapChain, "garage.demo.renderer");
        using var garage = new GarageSceneController(
            session.Device, session.Compiler, session.SwapChain.Format, WindowWidth, WindowHeight, lightingPreset);

        using var input = new DemoInputBindings(session.Window, garage, WindowWidth);
        var lifecycle = new MatchLifecycle();
        lifecycle.OutcomeChanged += ReportOutcome;

        garage.LoadAsset(assetPath, ResolvePrimaryShellAssetPath(shellVisuals));
        var sim = CreateMatch(matchComposition);
        garage.TankTint = schoolPalette.PaintFactor(sim.PlayerSchool);
        garage.OpponentTints = BuildOpponentTints(sim, schoolPalette);
        var casingEjector = new CasingEjector();
        var pauseCtl = new PauseController();
        pauseCtl.Changed += paused =>
            Console.Out.WriteLine(paused ? "[Garage demo] Paused — press P to resume." : "[Garage demo] Resumed.");
        var clock = new StopwatchClock();
        var lastSeconds = clock.GetElapsedSeconds();

        while (session.Window.IsOpen && !input.CloseRequested)
        {
            var now = clock.GetElapsedSeconds();
            var deltaSeconds = (float)Math.Max(0.0, now - lastSeconds);
            lastSeconds = now;

            if (input.RestartRequested)
            {
                input.ClearRestartRequest();
                sim = RestartMatch(sim, lifecycle, matchComposition);
                casingEjector.Reset();
                pauseCtl.Resume();
                garage.TankTint = schoolPalette.PaintFactor(sim.PlayerSchool);
                garage.OpponentTints = BuildOpponentTints(sim, schoolPalette);
            }

            if (input.PauseToggleRequested)
            {
                input.ClearPauseToggleRequest();
                pauseCtl.Toggle();
            }

            if (pauseCtl.IsPaused)
            {
                garage.Render(renderer);
                session.Window.PollEvents();
                continue;
            }

            sim.SubmitInput(input.Throttle, input.Steering, input.IsFireHeld);
            sim.Tick(deltaSeconds);

            if (lifecycle.Tick(sim.Outcome, deltaSeconds))
            {
                sim = RestartMatch(sim, lifecycle, matchComposition);
                casingEjector.Reset();
                garage.TankTint = schoolPalette.PaintFactor(sim.PlayerSchool);
                garage.OpponentTints = BuildOpponentTints(sim, schoolPalette);
            }

            ApplyPosesToScene(sim, garage, shellVisuals, casingEjector, (float)deltaSeconds);
            garage.Tick(deltaSeconds);
            garage.Render(renderer);
            session.Window.PollEvents();
        }

        sim.Dispose();
        return 0;
    }

    /// <summary>Builds a fresh demo match from the canonical <paramref name="composition"/>:
    /// the player spawn becomes the SimTankDriver's local-player tank, every opponent spawn
    /// fans out onto Team.OpponentSchool with the default <c>BotBrain</c> 60 m engage range.
    /// Spawn order matches the CSV row order so opponent indices are deterministic.</summary>
    private static SimTankDriver CreateMatch(MatchComposition composition)
    {
        var player = composition.Player;
        var playerSpec = TankRoster.FindById(player.TankId)
            ?? throw new InvalidOperationException(
                $"Match composition references unknown tank id \"{player.TankId}\" for the player spawn.");
        var driver = new SimTankDriver(playerSpec, player.Position, player.YawRadians);
        foreach (var opponent in composition.Opponents)
        {
            var spec = TankRoster.FindById(opponent.TankId)
                ?? throw new InvalidOperationException(
                    $"Match composition references unknown tank id \"{opponent.TankId}\" for an opponent spawn.");
            driver.SpawnOpponent(spec, opponent.Position, opponent.YawRadians);
        }

        return driver;
    }

    private static SimTankDriver RestartMatch(SimTankDriver current, MatchLifecycle lifecycle, MatchComposition composition)
    {
        current.Dispose();
        lifecycle.Reset();
        Console.Out.WriteLine("[Garage demo] Match restarted.");
        return CreateMatch(composition);
    }

    private static void ApplyPosesToScene(
        SimTankDriver sim,
        GarageSceneController garage,
        ShellVisualCatalog shellVisuals,
        CasingEjector casingEjector,
        float deltaSeconds)
    {
        garage.TankWorld = SimToWorldMapper.BuildTankWorld(
            sim.PlayerPositionXY, sim.PlayerYawRadians, sim.PlayerKnockedOutAtTick, sim.CurrentTick);
        garage.OpponentTanks = BuildOpponentWorlds(sim);
        if (sim.LatestSnapshot is { } snapshot)
        {
            garage.Projectiles = SimToWorldMapper.BuildProjectileTrail(snapshot.Projectiles);
            garage.ShellProjectiles = SimToWorldMapper.BuildShellHeads(snapshot.Projectiles, shellVisuals);
            casingEjector.Update(snapshot.Projectiles, CollectTankPoses(sim), deltaSeconds);
        }
        else
        {
            garage.Projectiles = Array.Empty<Matrix4x4>();
            garage.ShellProjectiles = Array.Empty<Matrix4x4>();
            casingEjector.Update(Array.Empty<Sim.Snapshot.ProjectileSnapshot>(), CollectTankPoses(sim), deltaSeconds);
        }

        garage.CasingProjectiles = casingEjector.CasingMatrices;
    }

    private static TankPose[] CollectTankPoses(SimTankDriver sim)
    {
        var count = 1 + sim.OpponentCount;
        var poses = new TankPose[count];
        poses[0] = new TankPose(sim.PlayerPositionXY, sim.PlayerYawRadians, !sim.IsPlayerKnockedOut);
        for (var i = 0; i < sim.OpponentCount; i++)
        {
            poses[i + 1] = new TankPose(
                sim.GetOpponentPosition(i),
                sim.GetOpponentYaw(i),
                !sim.IsOpponentKnockedOut(i));
        }

        return poses;
    }

    private static Matrix4x4[] BuildOpponentWorlds(SimTankDriver sim)
    {
        var count = sim.OpponentCount;
        if (count == 0)
        {
            return Array.Empty<Matrix4x4>();
        }

        var worlds = new Matrix4x4[count];
        for (var i = 0; i < count; i++)
        {
            worlds[i] = SimToWorldMapper.BuildTankWorld(
                sim.GetOpponentPosition(i),
                sim.GetOpponentYaw(i),
                sim.GetOpponentKnockedOutAtTick(i),
                sim.CurrentTick);
        }

        return worlds;
    }

    private static void ReportOutcome(MatchOutcome outcome)
    {
        switch (outcome)
        {
            case MatchOutcome.Victory:
                Console.Out.WriteLine("[Garage demo] VICTORY — every opponent knocked out.");
                break;
            case MatchOutcome.Defeat:
                Console.Out.WriteLine("[Garage demo] DEFEAT — player tank knocked out.");
                break;
        }
    }

    /// <summary>Derives the per-opponent tint array from each opponent's
    /// <see cref="SimTankDriver.GetOpponentSchool"/> + the loaded canon palette.
    /// No hardcoded school list — what each opponent's school is comes from its
    /// <see cref="TankSpec.School"/>, captured at spawn time inside the driver.</summary>
    private static Vector4[] BuildOpponentTints(SimTankDriver sim, SchoolPalette palette)
    {
        var count = sim.OpponentCount;
        var tints = new Vector4[count];
        for (var i = 0; i < count; i++)
        {
            tints[i] = palette.PaintFactor(sim.GetOpponentSchool(i));
        }

        return tints;
    }

    private static string ResolveDataPath(string relativePath) =>
        Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Resolves the catalog's AP-row VFS path to a real on-disk file under the
    /// bundled <c>content/</c> tree, or null when the catalog has no AP entry. Phase 7
    /// keys the demo's shell mesh by <see cref="AmmoType.AP"/> alone — every AP round
    /// renders with this single mesh until per-caliber assets land.</summary>
    private static string? ResolvePrimaryShellAssetPath(ShellVisualCatalog shellVisuals)
    {
        var spec = shellVisuals.Find(AmmoType.AP);
        return spec is null ? null : ResolveVfsPath(spec.ModelVfsPath);
    }

    private static string ResolveVfsPath(string vfsPath)
    {
        const string Scheme = "res://";
        if (!vfsPath.StartsWith(Scheme, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported VFS path \"{vfsPath}\" — expected \"{Scheme}…\" scheme.");
        }

        var relative = vfsPath[Scheme.Length..].Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(AppContext.BaseDirectory, "content", relative);
    }
}
