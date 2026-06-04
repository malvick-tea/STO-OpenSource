using System;
using System.Numerics;
using Opus.Engine.Renderer;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>Shared default lighting + post-fx for the client's offscreen 3D scenes (the
/// garage model view and the network match). One sun direction drives both the key light
/// and the sky gradient so the cast shadow and the sky agree on where light comes from;
/// ACES tonemap with bloom / colour-grading / AA off. Extracted so the garage renderer
/// and the match renderer share one definition instead of each carrying a copy.</summary>
internal static class SceneRenderDefaults
{
    private static readonly Vector3 SunDirectionWorld = Vector3.Normalize(new Vector3(0.4f, 0.85f, 0.55f));

    public static readonly LightingSetup Lighting = new(
        new DirectionalLight(
            DirectionWorld: SunDirectionWorld,
            Colour: new Vector3(1.0f, 0.95f, 0.85f),
            Intensity: 1f,
            CastsShadows: true),
        Array.Empty<LocalLight>(),
        new SkySetup(
            SunDirectionWorld: SunDirectionWorld,
            ZenithColour: new Vector3(0.35f, 0.45f, 0.62f),
            HorizonColour: new Vector3(0.78f, 0.71f, 0.60f),
            ExposureEv: 0f,
            EnvironmentMapHandle: 0));

    /// <summary>Quieter outdoor preset for the match. The garage keeps the brighter
    /// showroom lighting, while armour in battle retains texture detail instead of
    /// clipping under a full-strength sun plus blue ambient contribution.</summary>
    public static readonly LightingSetup MatchLighting = new(
        new DirectionalLight(
            DirectionWorld: SunDirectionWorld,
            Colour: new Vector3(1.00f, 0.94f, 0.82f),
            Intensity: 0.92f,
            CastsShadows: true),
        Array.Empty<LocalLight>(),
        new SkySetup(
            SunDirectionWorld: SunDirectionWorld,
            ZenithColour: new Vector3(0.18f, 0.22f, 0.30f),
            HorizonColour: new Vector3(0.30f, 0.34f, 0.40f),
            ExposureEv: 0f,
            EnvironmentMapHandle: 0));

    public static readonly PostFxSetup PostFx = new(
        Tonemap: TonemapOperator.AcesFilmic,
        Bloom: new BloomSetup(Enabled: false, Threshold: 1f, Intensity: 0f, MipChainLevels: 0),
        ColourGrading: new ColourGradingSetup(Enabled: false, LutHandle: 0, Saturation: 1f, Contrast: 1f),
        AntiAliasing: AntiAliasingMode.None,
        Upscale: UpscaleMode.None,
        ExposureEv: 0f);
}
