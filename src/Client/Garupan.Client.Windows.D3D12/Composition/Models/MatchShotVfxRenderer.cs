using System;
using System.Numerics;
using Garupan.Client.Ui.Match.Network;
using Garupan.Sim.Terrain;
using Opus.Engine.Pal.Filesystem;
using Opus.Engine.Renderer;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12;
using Opus.Engine.Ui.Text;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>Owns optional shot-sprite assets and projects transient muzzle flame, smoke, and
/// dust into the match UI composition. Kept separate from terrain and actor GPU submission.</summary>
internal sealed class MatchShotVfxRenderer : IDisposable
{
    private const string MuzzleFlashTexturePath = "res://vfx/kenney-particle-pack/muzzle_01.png";
    private const string SmokeTexturePath = "res://vfx/kenney-particle-pack/smoke_04.png";
    private const string DirtTexturePath = "res://vfx/kenney-particle-pack/dirt_03.png";

    private const float FlameLifetimeSeconds = 0.16f;
    private const float FlameOnsetBloom = 0.6f;
    private const float FlameCoreOffsetMeters = 0.05f;
    private const float FlameHotInnerOffsetMeters = 0.45f;
    private const float FlameHotOuterOffsetMeters = 0.85f;
    private const float FlameEdgeOffsetMeters = 1.20f;
    private const int FlameCoreSizePixels = 96;
    private const int FlameHotInnerSizePixels = 82;
    private const int FlameHotOuterSizePixels = 60;
    private const int FlameEdgeSizePixels = 42;

    private const int SmokePuffCount = 4;
    private const float SmokePuffStaggerSeconds = 0.16f;
    private const float SmokeBaseLiftMeters = 0.12f;
    private const float SmokeLiftPerPuffMeters = 0.05f;
    private const float SmokeAlphaLossPerPuff = 0.16f;
    private const int SmokeBaseSizePixels = 60;
    private const int SmokeGrowthPixels = 150;
    private const int SmokeTrailGrowthPixels = 22;
    private const byte SmokeMaxAlpha = 200;

    private const float DustLifetimeSeconds = 0.5f;
    private const int DustBaseSizePixels = 78;
    private const int DustGrowthPixels = 58;
    private const byte DustMaxAlpha = 150;
    private const byte OpaqueAlpha = 255;

    private static readonly Color FlameCoreColor = Color.FromRgb(255, 248, 224);
    private static readonly Color FlameHotColor = Color.FromRgb(255, 170, 72);
    private static readonly Color FlameEdgeColor = Color.FromRgb(232, 96, 30);
    private static readonly Color SmokeColor = Color.FromRgb(86, 82, 76);
    private static readonly Color DustColor = Color.FromRgb(122, 104, 78);

    private readonly D3D12DrawSurface _drawSurface;
    private readonly ShotVfxTexture _muzzleFlashTexture;
    private readonly ShotVfxTexture _smokeTexture;
    private readonly ShotVfxTexture _dirtTexture;
    private readonly ShotVfxTracker _tracker = new();
    private bool _disposed;

    private MatchShotVfxRenderer(
        D3D12DrawSurface drawSurface,
        ShotVfxTexture muzzleFlashTexture,
        ShotVfxTexture smokeTexture,
        ShotVfxTexture dirtTexture)
    {
        _drawSurface = drawSurface;
        _muzzleFlashTexture = muzzleFlashTexture;
        _smokeTexture = smokeTexture;
        _dirtTexture = dirtTexture;
    }

    /// <summary>Loads the optional CC0 sprite set. Missing assets keep asset-light builds
    /// functional without constructing a partial renderer.</summary>
    public static MatchShotVfxRenderer? TryLoad(
        D3D12RhiDevice device,
        D3D12DrawSurface drawSurface,
        IVfs vfs,
        string debugNamePrefix)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(drawSurface);
        ArgumentNullException.ThrowIfNull(vfs);
        ArgumentException.ThrowIfNullOrWhiteSpace(debugNamePrefix);
        if (!vfs.Exists(MuzzleFlashTexturePath) || !vfs.Exists(SmokeTexturePath) || !vfs.Exists(DirtTexturePath))
        {
            return null;
        }

        ShotVfxTexture? muzzle = null;
        ShotVfxTexture? smoke = null;
        ShotVfxTexture? dirt = null;
        try
        {
            muzzle = ShotVfxTexture.Load(device, vfs.Realize(MuzzleFlashTexturePath), $"{debugNamePrefix}.muzzle");
            smoke = ShotVfxTexture.Load(device, vfs.Realize(SmokeTexturePath), $"{debugNamePrefix}.smoke");
            dirt = ShotVfxTexture.Load(device, vfs.Realize(DirtTexturePath), $"{debugNamePrefix}.dirt");
            var renderer = new MatchShotVfxRenderer(drawSurface, muzzle, smoke, dirt);
            muzzle = null;
            smoke = null;
            dirt = null;
            return renderer;
        }
        finally
        {
            muzzle?.Dispose();
            smoke?.Dispose();
            dirt?.Dispose();
        }
    }

    public void Render(
        NetworkMatchScenePlan plan,
        FrameCameraSet cameras,
        int width,
        int height,
        TerrainHeightField? terrain)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var viewProjection = cameras.Main.View * cameras.Main.Projection;
        var bursts = _tracker.Resolve(plan);
        for (var i = 0; i < bursts.Count; i++)
        {
            DrawDust(bursts[i], viewProjection, width, height, terrain);
            DrawSmoke(bursts[i], viewProjection, width, height, terrain);
            DrawFlame(bursts[i], viewProjection, width, height, terrain);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _muzzleFlashTexture.Dispose();
        _smokeTexture.Dispose();
        _dirtTexture.Dispose();
    }

    private void DrawDust(
        in ShotVfxBurst burst,
        Matrix4x4 viewProjection,
        int width,
        int height,
        TerrainHeightField? terrain)
    {
        if (burst.AgeSeconds >= DustLifetimeSeconds)
        {
            return;
        }

        var progress = burst.AgeSeconds / DustLifetimeSeconds;
        DrawSprite(
            _dirtTexture,
            LiftPoint(burst.DustPosition, terrain),
            viewProjection,
            width,
            height,
            DustBaseSizePixels + (int)MathF.Round(progress * DustGrowthPixels),
            FadeAlpha(progress, DustMaxAlpha),
            DustColor);
    }

    private void DrawSmoke(
        in ShotVfxBurst burst,
        Matrix4x4 viewProjection,
        int width,
        int height,
        TerrainHeightField? terrain)
    {
        if (burst.AgeSeconds >= ShotVfxTracker.LifetimeSeconds)
        {
            return;
        }

        for (var puff = 0; puff < SmokePuffCount; puff++)
        {
            var gasAge = MathF.Max(0f, burst.AgeSeconds - (puff * SmokePuffStaggerSeconds));
            var progress = gasAge / ShotVfxTracker.LifetimeSeconds;
            var lift = SmokeBaseLiftMeters + (puff * SmokeLiftPerPuffMeters);
            var position = ShotVfxTracker.GasPosition(burst, gasAge) + (Vector3.UnitY * lift);
            var size = SmokeBaseSizePixels + (int)MathF.Round(progress * SmokeGrowthPixels) + (puff * SmokeTrailGrowthPixels);
            var alpha = (byte)(FadeAlpha(progress, SmokeMaxAlpha) * (1f - (puff * SmokeAlphaLossPerPuff)));
            DrawSprite(_smokeTexture, LiftPoint(position, terrain), viewProjection, width, height, size, alpha, SmokeColor);
        }
    }

    private void DrawFlame(
        in ShotVfxBurst burst,
        Matrix4x4 viewProjection,
        int width,
        int height,
        TerrainHeightField? terrain)
    {
        if (burst.AgeSeconds >= FlameLifetimeSeconds)
        {
            return;
        }

        var progress = burst.AgeSeconds / FlameLifetimeSeconds;
        var alpha = FadeAlpha(progress, OpaqueAlpha);
        var bloom = 1f + ((1f - progress) * FlameOnsetBloom);
        DrawFlameSprite(burst, terrain, viewProjection, width, height, FlameCoreOffsetMeters, FlameCoreSizePixels, bloom, alpha, FlameCoreColor);
        DrawFlameSprite(burst, terrain, viewProjection, width, height, FlameHotInnerOffsetMeters, FlameHotInnerSizePixels, bloom, alpha, FlameHotColor);
        DrawFlameSprite(burst, terrain, viewProjection, width, height, FlameHotOuterOffsetMeters, FlameHotOuterSizePixels, bloom, alpha, FlameHotColor);
        DrawFlameSprite(burst, terrain, viewProjection, width, height, FlameEdgeOffsetMeters, FlameEdgeSizePixels, bloom, alpha, FlameEdgeColor);
    }

    private void DrawFlameSprite(
        in ShotVfxBurst burst,
        TerrainHeightField? terrain,
        Matrix4x4 viewProjection,
        int width,
        int height,
        float offsetMeters,
        int baseSizePixels,
        float bloom,
        byte alpha,
        Color tint) =>
        DrawSprite(
            _muzzleFlashTexture,
            LiftPoint(ShotVfxTracker.FlashPlumePosition(burst, burst.AgeSeconds, offsetMeters), terrain),
            viewProjection,
            width,
            height,
            (int)MathF.Round(baseSizePixels * bloom),
            alpha,
            tint);

    private unsafe void DrawSprite(
        ShotVfxTexture texture,
        Vector3 world,
        Matrix4x4 viewProjection,
        int width,
        int height,
        int size,
        byte alpha,
        Color tint)
    {
        var anchor = WorldSpaceTextProjector.Project(world, viewProjection, width, height);
        if (!anchor.Visible || alpha == 0 || size <= 0)
        {
            return;
        }

        _drawSurface.DrawTexturedRect(
            texture.SrvTable,
            texture.SrvHeap,
            anchor.PixelX - (size / 2),
            anchor.PixelY - (size / 2),
            size,
            size,
            Color.FromRgba(tint.R, tint.G, tint.B, alpha));
    }

    private static Vector3 LiftPoint(Vector3 world, TerrainHeightField? terrain) =>
        terrain is null
            ? world
            : world + (Vector3.UnitY * terrain.HeightAt(world.X, world.Z));

    private static byte FadeAlpha(float progress, byte maximum) =>
        (byte)Math.Clamp((int)MathF.Round((1f - progress) * maximum), 0, maximum);
}
