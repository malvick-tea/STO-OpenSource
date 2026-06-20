using Garupan.Client.Core.Application;
using Garupan.Client.Core.Bootstrap;
using Garupan.Client.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Opus.Foundation;
using Opus.Persistence;

namespace Garupan.Client.Core.Composition;

/// <summary>
/// Bindings every Garupan client needs regardless of platform. Per-platform entries
/// (Client.Windows / Client.Android / Client.iOS) layer their own modules on top —
/// e.g. registering a Win-flavoured <see cref="Engine.Pal.Application.IWindowService"/>.
///
/// Keep this file boring: <c>services.AddSingleton/Scoped/Transient</c> only.
/// Logic, even if just helper composition, lives in dedicated modules co-located with
/// the feature that needs it.
/// </summary>
public static class CoreServicesModule
{
    public static void Register(IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton<BootSequence>();

        // Lifecycle channel — single instance, registered as both abstraction and concrete.
        services.AddSingleton<ExitService>();
        services.AddSingleton<IExitService>(sp => sp.GetRequiredService<ExitService>());

        // Cross-cutting infrastructure consumed by the persistence-backed services:
        // SettingsService + CampaignProgressService both frame their saves through
        // SaveHeaderSerializer + IBinaryCodec, stamp them with the running BuildInfo,
        // and timestamp them with IClock.
        services.AddSingleton<IBinaryCodec, MemoryPackCodec>();
        services.AddSingleton<ISaveIntegrityKeyProvider, MissingSaveIntegrityKeyProvider>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(BuildInfo.Current);

        // App-wide services. Constructed lazily by DI; first resolve happens during boot.
        services.AddSingleton<SettingsService>();
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<CampaignProgressService>();
    }
}
