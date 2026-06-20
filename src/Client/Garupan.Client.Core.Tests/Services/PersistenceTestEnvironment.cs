using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Opus.Foundation;
using Opus.Persistence;

namespace Garupan.Client.Core.Tests.Services;

/// <summary>Shared fixtures for the framed-blob persistence services
/// (<c>SettingsServiceTests</c>, <c>CampaignProgressServiceTests</c>). Both need an
/// <see cref="IClock"/> and a <see cref="BuildInfo"/> the test controls; lifting them
/// out keeps each suite focused on the service-specific semantics it's verifying.</summary>
internal static class PersistenceTestEnvironment
{
    /// <summary>Stable <see cref="BuildInfo"/> for tests — values are recognisable in
    /// failure messages and never collide with the real running build.</summary>
    public static readonly BuildInfo TestBuildInfo = new(
        Engine: EngineIdentity.Current,
        ProjectName: "STO.Tests",
        Version: new AppVersion(9, 8, 7, "test", "ci"),
        BuildConfiguration: "Debug",
        FrameworkDescription: ".NET 8.0.0",
        OperatingSystem: "Windows 11",
        ProcessArchitecture: "X64");

    public static readonly byte[] IntegrityKey =
        SHA256.HashData(Encoding.UTF8.GetBytes("garupan-persistence-tests"));

    public static readonly ISaveIntegrityKeyProvider IntegrityKeyProvider =
        new FixedSaveIntegrityKeyProvider(IntegrityKey);
}

internal sealed class FixedSaveIntegrityKeyProvider : ISaveIntegrityKeyProvider
{
    private readonly ReadOnlyMemory<byte> _key;

    public FixedSaveIntegrityKeyProvider(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _key = key;
    }

    public ValueTask<ReadOnlyMemory<byte>> GetKeyAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_key);
    }
}

/// <summary>Trivial deterministic <see cref="IClock"/> — tests set <see cref="Now"/>
/// directly when they need to pin a frame's creation timestamp.</summary>
internal sealed class FakeClock : IClock
{
    public long Now { get; set; }

    public long UtcUnixMilliseconds() => Now;
}
