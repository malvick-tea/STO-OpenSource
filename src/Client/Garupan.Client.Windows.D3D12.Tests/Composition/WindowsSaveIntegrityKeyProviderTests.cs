using System;
using System.IO;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Garupan.Client.Windows.Direct3D12.Composition;
using Garupan.Client.Windows.Direct3D12.Tests.Fixtures;
using Xunit;

namespace Garupan.Client.Windows.Direct3D12.Tests.Composition;

public sealed class WindowsSaveIntegrityKeyProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"garupan-save-key-{Guid.NewGuid():N}");

    [Fact]
    public async Task Provider_persists_one_dpapi_protected_key_per_install()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(_root);
        var vfs = new FakeVfs().Map("user://", _root);
        byte[] first;
        using (var provider = new WindowsSaveIntegrityKeyProvider(vfs))
        {
            first = (await provider.GetKeyAsync(CancellationToken.None)).ToArray();
        }

        byte[] second;
        using (var provider = new WindowsSaveIntegrityKeyProvider(vfs))
        {
            second = (await provider.GetKeyAsync(CancellationToken.None)).ToArray();
        }

        first.Should().HaveCount(32);
        second.Should().Equal(first);
        var protectedBytes = File.ReadAllBytes(
            Path.Combine(_root, "save-integrity.dpapi"));
        protectedBytes.Should().NotEqual(first);
    }

    [Fact]
    public async Task Provider_rejects_an_oversized_protected_key_file()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(_root);
        File.WriteAllBytes(
            Path.Combine(_root, "save-integrity.dpapi"),
            new byte[(16 * 1024) + 1]);
        var vfs = new FakeVfs().Map("user://", _root);
        using var provider = new WindowsSaveIntegrityKeyProvider(vfs);

        var act = async () =>
            await provider.GetKeyAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
