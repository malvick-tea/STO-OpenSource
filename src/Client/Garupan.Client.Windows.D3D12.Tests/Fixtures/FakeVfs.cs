using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Opus.Engine.Pal.Filesystem;

namespace Garupan.Client.Windows.Direct3D12.Tests.Fixtures;

/// <summary>Tiny <see cref="IVfs"/> stub that maps virtual paths to real OS paths.
/// Used by <see cref="D3D12ModelLoader"/> tests so the loader can resolve a bundled
/// glTF through the disk and the missing-path branch through a non-existent entry.
/// Write paths + stream IO throw — those are not exercised by these tests.</summary>
internal sealed class FakeVfs : IVfs
{
    private readonly Dictionary<string, string> _realPaths = new(StringComparer.Ordinal);

    public FakeVfs Map(string virtualPath, string realPath)
    {
        _realPaths[virtualPath] = realPath;
        return this;
    }

    public bool Exists(string virtualPath) =>
        _realPaths.TryGetValue(virtualPath, out var real) && File.Exists(real);

    public string Realize(string virtualPath) =>
        _realPaths.TryGetValue(virtualPath, out var real) ? real : virtualPath;

    public Stream OpenRead(string virtualPath) => throw new NotSupportedException();

    public Stream OpenWrite(string virtualPath) => throw new NotSupportedException();

    public Task WriteAllBytesAtomicAsync(string virtualPath, byte[] payload, CancellationToken ct) =>
        throw new NotSupportedException();
}
