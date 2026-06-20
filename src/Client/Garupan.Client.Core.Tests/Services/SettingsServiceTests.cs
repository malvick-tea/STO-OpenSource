using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Garupan.Client.Core.Application;
using Garupan.Client.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Opus.Engine.Input;
using Opus.Foundation;
using Opus.Persistence;
using Xunit;

namespace Garupan.Client.Core.Tests.Services;

/// <summary>Behavioural coverage for <see cref="SettingsService"/>'s binary-frame
/// persistence — round trip via <see cref="SaveHeaderSerializer"/>, fall-back on
/// corrupt frames, fall-back on schema-version mismatch, Apply mutation semantics.
/// Uses <see cref="InMemoryVfs"/> so no disk touches.</summary>
public sealed class SettingsServiceTests
{
    private static readonly BuildInfo TestBuildInfo = PersistenceTestEnvironment.TestBuildInfo;

    private static (SettingsService Service, InMemoryVfs Vfs, FakeClock Clock) BuildService(long initialMs = 1_700_000_000_000L)
    {
        var vfs = new InMemoryVfs();
        var clock = new FakeClock { Now = initialMs };
        var service = new SettingsService(
            vfs,
            new MemoryPackCodec(),
            clock,
            TestBuildInfo,
            PersistenceTestEnvironment.IntegrityKeyProvider,
            NullLogger<SettingsService>.Instance);
        return (service, vfs, clock);
    }

    [Fact]
    public async Task LoadAsync_with_no_file_keeps_defaults_and_writes_a_fresh_save()
    {
        var (service, vfs, _) = BuildService();

        await service.LoadAsync(CancellationToken.None);

        service.Current.Should().Be(AppSettings.Default);
        vfs.Files.Should().ContainKey(SettingsService.SettingsPath);
    }

    [Fact]
    public async Task SaveAsync_then_LoadAsync_round_trips_settings()
    {
        var (first, vfs, clock) = BuildService();
        await first.LoadAsync(CancellationToken.None);
        first.Apply(s => s with { Locale = "ru", MasterVolume = 0.25f, WindowWidth = 1920, WindowHeight = 1080 });

        var second = new SettingsService(
            vfs,
            new MemoryPackCodec(),
            clock,
            TestBuildInfo,
            PersistenceTestEnvironment.IntegrityKeyProvider,
            NullLogger<SettingsService>.Instance);
        await second.LoadAsync(CancellationToken.None);

        second.Current.Locale.Should().Be("ru");
        second.Current.MasterVolume.Should().Be(0.25f);
        second.Current.WindowWidth.Should().Be(1920);
        second.Current.WindowHeight.Should().Be(1080);
    }

    [Fact]
    public async Task Apply_change_fires_Changed_exactly_once()
    {
        var (service, _, _) = BuildService();
        await service.LoadAsync(CancellationToken.None);
        var firedCount = 0;
        service.Changed += _ => firedCount++;

        service.Apply(s => s with { VSync = !s.VSync });

        firedCount.Should().Be(1);
    }

    [Fact]
    public async Task Apply_no_op_does_not_fire_Changed()
    {
        var (service, _, _) = BuildService();
        await service.LoadAsync(CancellationToken.None);
        var firedCount = 0;
        service.Changed += _ => firedCount++;

        service.Apply(s => s);

        firedCount.Should().Be(0);
    }

    [Fact]
    public async Task Apply_mutation_is_observable_via_Current()
    {
        var (service, _, _) = BuildService();
        await service.LoadAsync(CancellationToken.None);

        service.Apply(s => s with { Locale = "ja" });

        service.Current.Locale.Should().Be("ja");
    }

    [Fact]
    public async Task LoadAsync_with_corrupt_payload_falls_back_to_defaults()
    {
        var vfs = new InMemoryVfs();
        await vfs.WriteAllBytesAtomicAsync(
            SettingsService.SettingsPath,
            System.Text.Encoding.UTF8.GetBytes("not a binary frame"),
            CancellationToken.None);
        var service = new SettingsService(
            vfs,
            new MemoryPackCodec(),
            new FakeClock(),
            TestBuildInfo,
            PersistenceTestEnvironment.IntegrityKeyProvider,
            NullLogger<SettingsService>.Instance);

        await service.LoadAsync(CancellationToken.None);

        service.Current.Should().Be(AppSettings.Default);
    }

    [Fact]
    public async Task LoadAsync_with_alien_magic_falls_back_to_defaults()
    {
        var vfs = new InMemoryVfs();
        var alienHeader = new SaveHeader("ALIEN", SchemaVersion: 1, TestBuildInfo.Version, CreatedAtUnixMs: 0);
        var alienFrame = SaveHeaderSerializer.WriteFrame(
            alienHeader,
            AppSettings.Default with { Locale = "fr" },
            new MemoryPackCodec(),
            PersistenceTestEnvironment.IntegrityKey);
        await vfs.WriteAllBytesAtomicAsync(SettingsService.SettingsPath, alienFrame, CancellationToken.None);
        var service = new SettingsService(
            vfs,
            new MemoryPackCodec(),
            new FakeClock(),
            TestBuildInfo,
            PersistenceTestEnvironment.IntegrityKeyProvider,
            NullLogger<SettingsService>.Instance);

        await service.LoadAsync(CancellationToken.None);

        service.Current.Should().Be(AppSettings.Default);
        service.Current.Locale.Should().NotBe("fr", "an alien-magic frame must not bleed through");
    }

    [Fact]
    public async Task LoadAsync_with_future_schema_version_falls_back_to_defaults()
    {
        var vfs = new InMemoryVfs();
        var futureHeader = SaveHeader.Current(schemaVersion: SaveSchemas.Settings + 100, TestBuildInfo.Version, unixMs: 0);
        var futureFrame = SaveHeaderSerializer.WriteFrame(
            futureHeader,
            AppSettings.Default with { Locale = "de" },
            new MemoryPackCodec(),
            PersistenceTestEnvironment.IntegrityKey);
        await vfs.WriteAllBytesAtomicAsync(SettingsService.SettingsPath, futureFrame, CancellationToken.None);
        var service = new SettingsService(
            vfs,
            new MemoryPackCodec(),
            new FakeClock(),
            TestBuildInfo,
            PersistenceTestEnvironment.IntegrityKeyProvider,
            NullLogger<SettingsService>.Instance);

        await service.LoadAsync(CancellationToken.None);

        service.Current.Should().Be(AppSettings.Default);
    }

    [Fact]
    public async Task Saved_frame_carries_current_schema_app_version_and_clock_timestamp()
    {
        var (service, vfs, clock) = BuildService(initialMs: 1_750_000_000_000L);
        await service.LoadAsync(CancellationToken.None);

        var blob = vfs.Files[SettingsService.SettingsPath];
        var read = SaveHeaderSerializer.ReadFrame<AppSettings>(
            blob,
            new MemoryPackCodec(),
            PersistenceTestEnvironment.IntegrityKey);

        read.IsOk.Should().BeTrue();
        var (header, body) = read.Unwrap();
        header.Magic.Should().Be(SaveHeader.MagicV1);
        header.SchemaVersion.Should().Be(SaveSchemas.Settings);
        header.AuthoringVersion.Should().Be(TestBuildInfo.Version);
        header.CreatedAtUnixMs.Should().Be(clock.Now);
        body.Should().Be(AppSettings.Default);
    }

    [Fact]
    public async Task Apply_persists_the_mutation_through_the_VFS()
    {
        var (service, vfs, _) = BuildService();
        await service.LoadAsync(CancellationToken.None);

        service.Apply(s => s with { SfxVolume = 0.42f });

        // Apply kicks off a fire-and-forget SaveAsync; await the in-memory write completes synchronously here.
        await service.SaveAsync(CancellationToken.None);
        var blob = vfs.Files[SettingsService.SettingsPath];
        var read = SaveHeaderSerializer.ReadFrame<AppSettings>(
            blob,
            new MemoryPackCodec(),
            PersistenceTestEnvironment.IntegrityKey);

        read.IsOk.Should().BeTrue();
        read.Unwrap().Body.SfxVolume.Should().Be(0.42f);
    }

    [Fact]
    public async Task LoadAsync_advances_clock_observed_timestamp_in_fresh_save()
    {
        var (service, vfs, clock) = BuildService(initialMs: 1_000_000L);
        clock.Now = 2_000_000L;

        await service.LoadAsync(CancellationToken.None);
        var blob = vfs.Files[SettingsService.SettingsPath];
        var read = SaveHeaderSerializer.ReadFrame<AppSettings>(
            blob,
            new MemoryPackCodec(),
            PersistenceTestEnvironment.IntegrityKey);

        read.Unwrap().Header.CreatedAtUnixMs.Should().Be(2_000_000L);
    }

    [Fact]
    public async Task SaveAsync_then_LoadAsync_round_trips_rebound_input_bindings()
    {
        var (first, vfs, clock) = BuildService();
        await first.LoadAsync(CancellationToken.None);
        first.Apply(s => s with { Bindings = s.Bindings with { MoveForward = Key.I, Fire = Key.Enter } });

        var second = new SettingsService(
            vfs,
            new MemoryPackCodec(),
            clock,
            TestBuildInfo,
            PersistenceTestEnvironment.IntegrityKeyProvider,
            NullLogger<SettingsService>.Instance);
        await second.LoadAsync(CancellationToken.None);

        second.Current.Bindings.MoveForward.Should().Be(Key.I);
        second.Current.Bindings.Fire.Should().Be(Key.Enter);
        second.Current.Bindings.SteerLeft.Should().Be(Key.A, "an untouched binding keeps its default");
    }

    [Fact]
    public async Task SaveAsync_then_LoadAsync_round_trips_multiplayer_endpoint()
    {
        var (first, vfs, clock) = BuildService();
        await first.LoadAsync(CancellationToken.None);
        first.Apply(s => s with { Multiplayer = new MultiplayerSettings { Host = "alpha.example.test", Port = 9123 } });

        var second = new SettingsService(
            vfs,
            new MemoryPackCodec(),
            clock,
            TestBuildInfo,
            PersistenceTestEnvironment.IntegrityKeyProvider,
            NullLogger<SettingsService>.Instance);
        await second.LoadAsync(CancellationToken.None);

        second.Current.Multiplayer.Host.Should().Be("alpha.example.test");
        second.Current.Multiplayer.Port.Should().Be(9123);
    }

    [Fact]
    public void Default_multiplayer_endpoint_is_loopback_and_canonical_port()
    {
        var defaults = AppSettings.Default;

        defaults.Multiplayer.Host.Should().Be(MultiplayerSettings.LoopbackHost);
        defaults.Multiplayer.Port.Should().Be(MultiplayerSettings.DefaultPort);
    }
}
