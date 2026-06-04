using System.Threading;
using System.Threading.Tasks;
using Garupan.Client.Core.Bootstrap;
using Garupan.Client.Core.Services;
using Microsoft.Extensions.Logging;
using Opus.Engine.Audio;

namespace Garupan.Client.Windows.Bootstrap;

/// <summary>
/// Audio boot stage. Order 130 — after Localization (120), before InitialScreen (1100)
/// so the menu screen can play its enter sfx the moment it appears. Subscribes the
/// <see cref="AudioMixer"/> to <see cref="SettingsService.Changed"/> so the volume
/// sliders take effect live without a reboot.
/// </summary>
public sealed class AudioStage : IBootStage
{
    private readonly IAudioDevice _audio;
    private readonly SettingsService _settings;
    private readonly ILogger<AudioStage> _logger;

    public AudioStage(IAudioDevice audio, SettingsService settings, ILogger<AudioStage> logger)
    {
        _audio = audio;
        _settings = settings;
        _logger = logger;
    }

    public string Name => "Audio";

    public int Order => 130;

    public Task ExecuteAsync(BootContext ctx, CancellationToken ct)
    {
        _ = ctx;
        _ = ct;

        ApplyFromSettings();
        _settings.Changed += OnSettingsChanged;

        if (_audio.IsReady)
        {
            _logger.LogInformation(
                "Audio stage applied gains from settings: master={Master:0.00} music={Music:0.00} sfx={Sfx:0.00}",
                _audio.Mixer.MasterGain,
                _audio.Mixer.MusicGain,
                _audio.Mixer.SfxGain);
        }
        else
        {
            _logger.LogWarning("Audio device not ready — gain updates will be silent.");
        }

        return Task.CompletedTask;
    }

    private void OnSettingsChanged(Garupan.Client.Core.Application.AppSettings settings)
    {
        _ = settings;
        ApplyFromSettings();
    }

    private void ApplyFromSettings()
    {
        var s = _settings.Current;
        _audio.Mixer.Set(s.MasterVolume, s.MusicVolume, s.SfxVolume);
    }
}
