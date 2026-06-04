using System;
using Opus.Engine.Audio;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>How quickly a looping channel ramps its gain, expressed as the seconds it takes to
/// traverse the full <c>[0, 1]</c> range. Wind-down is intentionally slower than spin-up: a real
/// drivetrain coasts down to silence rather than being switched off instantly.</summary>
internal readonly record struct FadeProfile(float FadeInSeconds, float FadeOutSeconds)
{
    public float FadeInPerSecond => 1f / FadeInSeconds;

    public float FadeOutPerSecond => 1f / FadeOutSeconds;
}

/// <summary>
/// One looping engine / track / turret channel that ramps its gain toward a target instead of
/// snapping, so motors audibly spin up and wind down the way a real powertrain does. Callers only
/// ever set a target volume (0 winds the loop down); the channel starts lazily the moment it leaves
/// silence and stops itself once a wind-down has faded back to inaudible.
/// </summary>
internal sealed class FadingSfxLoop : IDisposable
{
    private const float SilenceEpsilon = 0.001f;

    private readonly ILoopingSfxPlayer _loops;
    private readonly string _path;
    private readonly float _fadeInPerSecond;
    private readonly float _fadeOutPerSecond;

    private ILoopingSfxHandle? _handle;
    private float _current;
    private float _target;

    public FadingSfxLoop(ILoopingSfxPlayer loops, string path, FadeProfile fade)
    {
        _loops = loops;
        _path = path;
        _fadeInPerSecond = fade.FadeInPerSecond;
        _fadeOutPerSecond = fade.FadeOutPerSecond;
    }

    /// <summary>True while the underlying channel is live (audible or mid-fade), false once it has
    /// wound all the way down and released its handle.</summary>
    public bool IsAudible => _handle is { IsPlaying: true };

    /// <summary>Sets the gain the channel should ramp toward. Zero winds it down to silence.</summary>
    public void SetTarget(float volume) => _target = MathF.Max(0f, volume);

    /// <summary>Advances the live gain toward the target by one frame. Starts the loop the instant it
    /// leaves silence and disposes it once a wind-down reaches zero, so a stopped tank's engine fades
    /// out over <see cref="FadeProfile.FadeOutSeconds"/> rather than cutting.</summary>
    public void Advance(float deltaSeconds)
    {
        if (_current < _target)
        {
            _current = MathF.Min(_target, _current + (_fadeInPerSecond * deltaSeconds));
        }
        else if (_current > _target)
        {
            _current = MathF.Max(_target, _current - (_fadeOutPerSecond * deltaSeconds));
        }

        if (_current <= SilenceEpsilon && _target <= SilenceEpsilon)
        {
            StopImmediately();
            return;
        }

        if (_handle is null || !_handle.IsPlaying)
        {
            _handle?.Dispose();
            _handle = _loops.PlayLoop(_path, _current);
            return;
        }

        _handle.SetVolume(_current);
    }

    /// <summary>Cuts the channel without a fade — for match teardown / disposal, where no further
    /// frames will run to carry a wind-down to completion.</summary>
    public void StopImmediately()
    {
        _handle?.Stop();
        _handle?.Dispose();
        _handle = null;
        _current = 0f;
        _target = 0f;
    }

    public void Dispose() => StopImmediately();
}
