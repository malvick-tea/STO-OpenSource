using Garupan.Client.Core.Bootstrap;
using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Match.Network;
using Garupan.Client.Ui.Navigation;
using Garupan.Client.Windows.Bootstrap;
using Garupan.Client.Windows.Direct3D12.Composition.Models;
using Garupan.Content;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Opus.Engine.Audio;
using Opus.Engine.Audio.Raylib;
using Opus.Engine.Input;
using Opus.Engine.Input.Sdl3;
using Opus.Engine.Pal.Application;
using Opus.Engine.Pal.Filesystem;
using Opus.Engine.Pal.Hardware;
using Opus.Engine.Pal.PowerManagement;
using Opus.Engine.Pal.Process;
using Opus.Engine.Pal.Sdl3;
using Opus.Engine.Pal.Threading;
using Opus.Engine.Pal.Time;
using Opus.Engine.Pal.Windows.Application;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Pal.Windows.Filesystem;
using Opus.Engine.Pal.Windows.Hardware;
using Opus.Engine.Pal.Windows.PowerManagement;
using Opus.Engine.Pal.Windows.Process;
using Opus.Engine.Pal.Windows.Threading;
using Opus.Engine.Pal.Windows.Time;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12;

namespace Garupan.Client.Windows.Direct3D12.Composition;

/// <summary>
/// DI bindings for the sole (D3D12) client — the boot pipeline + screens resolve their
/// host services here:
/// <see cref="IWindowService"/> → <see cref="SdlWindowService"/> (from <see cref="D3D12HostBundle"/>),
/// <see cref="IDrawSurface"/> → <see cref="D3D12DrawSurface"/>,
/// <see cref="IInputSource"/> → <see cref="SdlPolledInputSource"/>.
///
/// <para>3D scene path (IModelLoader / IModelRenderer) runs on the runtime D3D12
/// renderer + the offscreen <c>D3D12SceneViewport</c> from Phase D2a: each garage / 3D
/// screen call composites its scene as a textured-RGBA quad on top of the UI batch via
/// alpha-aware compositing. Audio routes through the engine's Raylib/miniaudio backend.</para>
/// </summary>
internal static class D3D12WindowsServicesModule
{
    public static void Register(IServiceCollection services, MainThreadDispatcher dispatcher)
    {
        services.AddSingleton<IMainThreadDispatcher>(dispatcher);
        services.AddSingleton(dispatcher);

        RegisterD3D12Host(services);
        RegisterPlatformPal(services);
        RegisterAudio(services);
        RegisterContentCatalogs(services);
        RegisterBootStages(services);
    }

    /// <summary>D3D12 host bundle + the engine-contract façades that resolve through it.
    /// The container disposes the bundle on shutdown so the swap chain + atlas + draw
    /// surface tear down in dependency order while the device is still alive.</summary>
    private static void RegisterD3D12Host(IServiceCollection services)
    {
        services.AddSingleton<D3D12HostBundle>(sp => D3D12HostBundle.Open(
            sp.GetRequiredService<IVfs>(),
            sp.GetRequiredService<SettingsService>().Current));
        services.AddSingleton<IWindowService>(sp => sp.GetRequiredService<D3D12HostBundle>().Session.Window);
        services.AddSingleton<SdlWindowService>(sp => sp.GetRequiredService<D3D12HostBundle>().Session.Window);
        services.AddSingleton<IDrawSurface>(sp => sp.GetRequiredService<D3D12HostBundle>().DrawSurface);
        services.AddSingleton<D3D12DrawSurface>(sp => sp.GetRequiredService<D3D12HostBundle>().DrawSurface);
        services.AddSingleton<D3D12UiFrameLoop>(sp => sp.GetRequiredService<D3D12HostBundle>().FrameLoop);
        services.AddSingleton<SdlPolledInputSource>(sp => sp.GetRequiredService<D3D12HostBundle>().Input);
        services.AddSingleton<IInputSource>(sp => sp.GetRequiredService<SdlPolledInputSource>());
        services.AddSingleton<IMouseModeService>(sp => sp.GetRequiredService<SdlPolledInputSource>());

        // 3D model path: runtime D3D12 adapters (Phase D2b). The loader caches glTF
        // assets per path and decodes the embedded base-colour image into a
        // SingleTextureAtlas; the renderer composes the scene through an offscreen
        // D3D12SceneViewport and composites the alpha-aware result into the UI draw
        // surface via DrawTexturedRect.
        services.AddSingleton<D3D12ModelLoader>(sp => new D3D12ModelLoader(
            sp.GetRequiredService<D3D12HostBundle>().Session.Device,
            sp.GetRequiredService<IVfs>(),
            sp.GetRequiredService<ILogger<D3D12ModelLoader>>()));
        services.AddSingleton<IModelLoader>(sp => sp.GetRequiredService<D3D12ModelLoader>());
        services.AddSingleton<D3D12ModelRenderer>(sp =>
        {
            var bundle = sp.GetRequiredService<D3D12HostBundle>();
            return new D3D12ModelRenderer(
                bundle.Session.Device,
                bundle.Session.Compiler,
                bundle.Session.SwapChain,
                bundle.DrawSurface);
        });
        services.AddSingleton<IModelRenderer>(sp => sp.GetRequiredService<D3D12ModelRenderer>());

        // Network-match 3D scene renderer (tank + ground + sky). Distinct from the bare
        // IModelRenderer above so the match gets a floor while the garage stays floor-less.
        services.AddSingleton<D3D12MatchSceneRenderer>(sp =>
        {
            var bundle = sp.GetRequiredService<D3D12HostBundle>();
            // The local tank's sound set is data-driven (data/tank-audio.csv): resolve the profile
            // from the authored default rather than baking a showcase tank id or paths into code. The tracker plays
            // the cannon shot, engine, tracks, and turret from the projected match snapshot.
            var tankAudio = new TankAudioTracker(
                sp.GetRequiredService<ISfxPlayer>(),
                sp.GetRequiredService<ILoopingSfxPlayer>(),
                TankAudioCatalog.RequireDefault());
            return new D3D12MatchSceneRenderer(
                bundle.Session.Device,
                bundle.Session.Compiler,
                bundle.Session.SwapChain,
                bundle.DrawSurface,
                sp.GetRequiredService<IVfs>(),
                sp.GetRequiredService<BattleMapCatalog>(),
                tankAudio);
        });
        services.AddSingleton<IMatchSceneRenderer>(sp => sp.GetRequiredService<D3D12MatchSceneRenderer>());
        services.AddSingleton<ScreenStack>();
    }

    /// <summary>Windows-specific PAL implementations + Foundation-tier infrastructure that
    /// the boot pipeline + screen stack need (clock, VFS, lifecycle, hardware, crash UI).
    /// The PAL is renderer-agnostic — none of these depend on the draw backend.</summary>
    private static void RegisterPlatformPal(IServiceCollection services)
    {
        services.AddSingleton<IVfs>(_ => WindowsVfs.ForCurrentProcess("STO"));
        services.AddSingleton<ILifecycleService, WindowsLifecycleService>();
        services.AddSingleton<IJobScheduler, ThreadPoolJobScheduler>();
        services.AddSingleton<IHighResClock, StopwatchClock>();
        services.AddSingleton<IProcessInfo, WindowsProcessInfo>();
        services.AddSingleton<IHardwareInfo>(sp =>
            new WindowsHardwareInfo(sp.GetRequiredService<IVfs>().Realize("user://")));
        services.AddSingleton<IThermalState, WindowsThermalState>();
        services.AddSingleton<IBatteryState, WindowsBatteryState>();
        services.AddSingleton<ICrashHandler>(sp =>
            new WindowsCrashHandler(sp.GetRequiredService<IVfs>().Realize("user://crashes")));
        services.AddSingleton<ICrashReportPresenter, WindowsMessageBoxCrashPresenter>();
        services.AddSingleton<ICrashRestartLauncher, WindowsRestartLauncher>();
        services.AddSingleton<CrashReportNotifier>();
    }

    /// <summary>Raylib/miniaudio output for short SFX and managed loop channels. Music remains
    /// on its inert implementation until the menu soundtrack lands.</summary>
    private static void RegisterAudio(IServiceCollection services)
    {
        services.AddSingleton<AudioMixer>(_ => new AudioMixer(masterGain: 0.8f, musicGain: 0.7f, sfxGain: 0.9f));
        services.AddSingleton<RaylibAudioDevice>();
        services.AddSingleton<IAudioDevice>(sp => sp.GetRequiredService<RaylibAudioDevice>());
        services.AddSingleton<IMusicPlayer, NullMusicPlayer>();
        services.AddSingleton<RaylibSfxPlayer>(sp =>
            new RaylibSfxPlayer(sp.GetRequiredService<RaylibAudioDevice>(), sp.GetRequiredService<IVfs>().Realize));
        services.AddSingleton<ISfxPlayer>(sp => sp.GetRequiredService<RaylibSfxPlayer>());
        services.AddSingleton<RaylibLoopingSfxPlayer>(sp =>
            new RaylibLoopingSfxPlayer(sp.GetRequiredService<RaylibAudioDevice>(), sp.GetRequiredService<IVfs>().Realize));
        services.AddSingleton<ILoopingSfxPlayer>(sp => sp.GetRequiredService<RaylibLoopingSfxPlayer>());
    }

    /// <summary>Canon content catalogs loaded lazily from the bundled CSVs (the data is
    /// renderer-agnostic).</summary>
    private static void RegisterContentCatalogs(IServiceCollection services)
    {
        services.AddSingleton<CampaignSpec>(sp =>
            SampleCampaign.Load(sp.GetRequiredService<IVfs>().Realize("res://campaigns/sample.csv")));
        services.AddSingleton<CrewRoster>(sp =>
            PlayerTeam.Load(sp.GetRequiredService<IVfs>().Realize("res://crews/player_crew.csv")));
        services.AddSingleton<MatchModeCatalog>(sp =>
            MatchModeCsv.LoadFile(sp.GetRequiredService<IVfs>().Realize("res://match-modes.csv")));
        services.AddSingleton<BattleMapCatalog>(sp =>
            BattleMapCsv.LoadFile(sp.GetRequiredService<IVfs>().Realize(BattleMapVfsPaths.CatalogPath)));
    }

    /// <summary>The boot pipeline. Order numbers on each stage's <c>IBootStage.Order</c>
    /// property control execution sequence — registration order here is irrelevant.</summary>
    private static void RegisterBootStages(IServiceCollection services)
    {
        services.AddSingleton<IBootStage, ConfigurationStage>();
        services.AddSingleton<IBootStage, EngineInitStage>();
        services.AddSingleton<IBootStage, SplashPushStage>();
        services.AddSingleton<IBootStage, LocalizationStage>();
        services.AddSingleton<IBootStage, CampaignDataStage>();
        services.AddSingleton<IBootStage, CrewDataStage>();
        services.AddSingleton<IBootStage, MatchModeDataStage>();
        services.AddSingleton<IBootStage, CampaignProgressStage>();
        services.AddSingleton<IBootStage, AudioStage>();
        services.AddSingleton<IBootStage, BannerStage>();
        services.AddSingleton<IBootStage, InitialScreenStage>();
    }
}
