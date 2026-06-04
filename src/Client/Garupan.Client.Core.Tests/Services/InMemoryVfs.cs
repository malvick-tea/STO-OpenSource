using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Opus.Engine.Pal.Filesystem;

namespace Garupan.Client.Core.Tests.Services;

/// <summary>Tiny in-memory <see cref="IVfs"/> used by service tests so a real disk
/// isn't touched. Stores each written payload as a byte[] keyed by virtual path;
/// rewrite replaces the stored bytes atomically (no temp-rename gymnastics).</summary>
internal sealed class InMemoryVfs : IVfs
{
    private readonly Dictionary<string, byte[]> _files = new(System.StringComparer.Ordinal);

    public IReadOnlyDictionary<string, byte[]> Files => _files;

    public bool Exists(string virtualPath) => _files.ContainsKey(virtualPath);

    public Stream OpenRead(string virtualPath)
    {
        if (!_files.TryGetValue(virtualPath, out var body))
        {
            throw new FileNotFoundException($"In-memory VFS has no entry for {virtualPath}");
        }

        return new MemoryStream(body, writable: false);
    }

    public Stream OpenWrite(string virtualPath)
    {
        var buffer = new MemoryStream();
        _files[virtualPath] = System.Array.Empty<byte>();
        return new TrackingStream(buffer, body => _files[virtualPath] = body);
    }

    public Task WriteAllBytesAtomicAsync(string virtualPath, byte[] payload, CancellationToken ct)
    {
        _ = ct;
        _files[virtualPath] = (byte[])payload.Clone();
        return Task.CompletedTask;
    }

    public string Realize(string virtualPath) => virtualPath;

    private sealed class TrackingStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly System.Action<byte[]> _onClose;

        public TrackingStream(MemoryStream inner, System.Action<byte[]> onClose)
        {
            _inner = inner;
            _onClose = onClose;
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            _inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _onClose(_inner.ToArray());
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
