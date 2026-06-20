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

    /// <summary>Narrow finalizer that releases only the unmanaged COM pointer
    /// held by <see cref="_srvHeap"/>. The <see cref="D3D12Texture"/> field
    /// has its own finalizer and is left to the GC. The finalizer must not
    /// touch any managed object (it runs on the GC thread and the texture's
    /// finalizer may already have executed), and must not perform a GPU wait
    /// (the device may already be gone). It only releases the descriptor
    /// heap, which is a pure COM Release with no driver round-trip.</summary>
    ~ShotVfxTexture()
    {
        ReleaseSrvHeap();
    }

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
        D3D12Texture? texture = null;
        ID3D12DescriptorHeap* srvHeap = null;
        try
        {
            texture = device.CreateGraphicsTexture(new RhiTextureDescription(
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

            srvHeap = device.CreateSrvDescriptorHeap(1u);
            var srvTable = device.CreateShaderResourceView(texture, srvHeap);
            var result = new ShotVfxTexture(texture, srvHeap, srvTable);
            texture = null;
            srvHeap = null;
            return result;
        }
        finally
        {
            if (srvHeap != null)
            {
                srvHeap->Release();
            }

            texture?.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
        ReleaseSrvHeap();
        _texture.Dispose();
    }

    private void ReleaseSrvHeap()
    {
        var heap = _srvHeap;
        if (heap == null)
        {
            return;
        }

        _srvHeap = null;
        heap->Release();
    }
}
