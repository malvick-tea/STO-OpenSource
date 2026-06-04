using System;
using System.IO;
using Opus.Content.Textures;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>One uploaded RGBA sprite and its shader-visible SRV heap. Match muzzle VFX
/// keep three instances alive for the renderer lifetime and release them on shutdown.</summary>
internal sealed unsafe class ShotVfxTexture : IDisposable
{
    private readonly D3D12Texture _texture;
    private ID3D12DescriptorHeap* _srvHeap;
    private bool _disposed;

    private ShotVfxTexture(D3D12Texture texture, ID3D12DescriptorHeap* srvHeap, GpuDescriptorHandle srvTable)
    {
        _texture = texture;
        _srvHeap = srvHeap;
        SrvTable = srvTable;
    }

    public ID3D12DescriptorHeap* SrvHeap => _srvHeap;

    public GpuDescriptorHandle SrvTable { get; }

    public static ShotVfxTexture Load(D3D12RhiDevice device, string path, string debugName)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(debugName);

        var decoded = ImageDecoder.DecodeRgba8(File.ReadAllBytes(path));
        var texture = device.CreateGraphicsTexture(new RhiTextureDescription(
            debugName,
            decoded.Width,
            decoded.Height,
            1,
            RhiTextureFormat.Rgba8Unorm,
            RhiTextureUsage.Sampled));
        using (var commandList = device.CreateGraphicsCommandList($"{debugName}.upload"))
        {
            commandList.Begin(0);
            using var staging = device.ScheduleTextureUpload(texture, decoded.Rgba, commandList);
            commandList.End();
            commandList.ExecuteOn(device);
            device.WaitForIdle();
        }

        var srvHeap = device.CreateSrvDescriptorHeap(1u);
        var srvTable = device.CreateShaderResourceView(texture, srvHeap);
        return new ShotVfxTexture(texture, srvHeap, srvTable);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_srvHeap != null)
        {
            _srvHeap->Release();
            _srvHeap = null;
        }

        _texture.Dispose();
    }
}
