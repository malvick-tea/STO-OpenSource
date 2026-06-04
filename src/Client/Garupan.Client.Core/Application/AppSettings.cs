using MemoryPack;

namespace Garupan.Client.Core.Application;

/// <summary>
/// User-facing settings, persisted as a length-prefixed MemoryPack frame to
/// <c>user://settings.gsav</c>. Plain record — no validation logic here, that's
/// <see cref="Services.SettingsService"/>'s job. <see cref="MemoryPackableAttribute"/>
/// source-generates the binary serializer; pair with
/// <see cref="Opus.Persistence.SaveHeaderSerializer"/> for the on-disk header +
/// body framing.
/// </summary>
/// <remarks>
/// Every property carries a default so an older binary save (with fewer fields) loads
/// forward as defaulted blanks. A true schema break bumps
/// <see cref="SaveSchemas.Settings"/> and routes through the
/// migration pipeline (planned).
/// </remarks>
[MemoryPackable]
public sealed partial record AppSettings
{
    public string Locale { get; init; } = "en";

    public int WindowWidth { get; init; } = 1280;

    public int WindowHeight { get; init; } = 720;

    public bool VSync { get; init; } = true;

    public float MasterVolume { get; init; } = 0.8f;

    public float MusicVolume { get; init; } = 0.7f;

    public float SfxVolume { get; init; } = 0.9f;

    /// <summary>Rebindable match keyboard controls. See <see cref="InputBindings"/>.</summary>
    public InputBindings Bindings { get; init; } = InputBindings.Default;

    /// <summary>Local test match server target. See <see cref="MultiplayerSettings"/>.</summary>
    public MultiplayerSettings Multiplayer { get; init; } = MultiplayerSettings.Default;

    public static AppSettings Default => new();
}
