using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using Garupan.Client.Core.Application;
using Garupan.Client.Core.Services;
using Garupan.Content;
using Microsoft.Extensions.Logging.Abstractions;
using Opus.Foundation;
using Opus.Persistence;
using Xunit;

namespace Garupan.Client.Core.Tests.Services;

/// <summary>Behavioural coverage for <see cref="CampaignProgressService"/> — unlock
/// semantics over canonical prereq graphs, save / load round trip, idempotent
/// mark-complete, last-played tracking, binary-frame corruption / schema / campaign
/// mismatch fall-back. Uses an in-memory <c>IVfs</c> so no disk touches.</summary>
public sealed class CampaignProgressServiceTests
{
    private static readonly BuildInfo TestBuildInfo = PersistenceTestEnvironment.TestBuildInfo;

    private static CampaignSpec BuildToySpec()
    {
        var missions = new[]
        {
            MissionSpec.Of("toy.a", "t.a.title", "Ep 1", OpponentSchool.PlayerSchool, MissionEnvironment.RuralOpen, MissionObjective.Bracket, "t.a.summary", "t.a.briefing", "scripted/a"),
            MissionSpec.Of("toy.b", "t.b.title", "Ep 2", OpponentSchool.RivalBravo, MissionEnvironment.ForestedHills, MissionObjective.KnockoutAll, "t.b.summary", "t.b.briefing", "scripted/b"),
            MissionSpec.Of("toy.c", "t.c.title", "Ep 3", OpponentSchool.RivalDelta, MissionEnvironment.SnowyVillage, MissionObjective.BreakOut, "t.c.summary", "t.c.briefing", "scripted/c"),
        };
        var nodes = new[]
        {
            new CampaignNode("toy.a", 0.1f, 0.5f, System.Array.Empty<string>()),
            new CampaignNode("toy.b", 0.3f, 0.5f, new[] { "toy.a" }),
            new CampaignNode("toy.c", 0.6f, 0.5f, new[] { "toy.a", "toy.b" }),
        };
        return new CampaignSpec("toy_campaign", "campaign.toy.name", "campaign.toy.subtitle", missions, nodes);
    }

    private static (CampaignProgressService Service, InMemoryVfs Vfs, CampaignSpec Spec) BuildService()
    {
        var vfs = new InMemoryVfs();
        var spec = BuildToySpec();
        var service = BuildServiceFor(vfs, spec);
        return (service, vfs, spec);
    }

    private static CampaignProgressService BuildServiceFor(InMemoryVfs vfs, CampaignSpec spec) =>
        new(
            vfs,
            spec,
            new MemoryPackCodec(),
            new FakeClock(),
            TestBuildInfo,
            NullLogger<CampaignProgressService>.Instance);

    [Fact]
    public async System.Threading.Tasks.Task Fresh_profile_unlocks_only_the_no_prereq_missions()
    {
        var (service, _, spec) = BuildService();
        await service.LoadAsync(CancellationToken.None);

        service.IsUnlocked("toy.a", spec).Should().BeTrue();
        service.IsUnlocked("toy.b", spec).Should().BeFalse();
        service.IsUnlocked("toy.c", spec).Should().BeFalse();
    }

    [Fact]
    public async System.Threading.Tasks.Task MarkComplete_unlocks_dependent_mission()
    {
        var (service, _, spec) = BuildService();
        await service.LoadAsync(CancellationToken.None);

        service.MarkComplete("toy.a");
        service.IsUnlocked("toy.b", spec).Should().BeTrue();
        service.IsUnlocked("toy.c", spec).Should().BeFalse();
    }

    [Fact]
    public async System.Threading.Tasks.Task Multi_prereq_mission_unlocks_only_when_all_prereqs_complete()
    {
        var (service, _, spec) = BuildService();
        await service.LoadAsync(CancellationToken.None);

        service.MarkComplete("toy.a");
        service.IsUnlocked("toy.c", spec).Should().BeFalse(
            "toy.c also needs toy.b complete");

        service.MarkComplete("toy.b");
        service.IsUnlocked("toy.c", spec).Should().BeTrue();
    }

    [Fact]
    public async System.Threading.Tasks.Task MarkComplete_is_idempotent_for_repeated_calls()
    {
        var (service, _, _) = BuildService();
        await service.LoadAsync(CancellationToken.None);
        var firedCount = 0;
        service.Changed += _ => firedCount++;

        service.MarkComplete("toy.a");
        service.MarkComplete("toy.a");

        service.Current.CompletedMissionIds.Should().BeEquivalentTo(new[] { "toy.a" });
        firedCount.Should().Be(1, "second MarkComplete for the same mission is a no-op");
    }

    [Fact]
    public async System.Threading.Tasks.Task MarkComplete_updates_last_played_id_each_time()
    {
        var (service, _, _) = BuildService();
        await service.LoadAsync(CancellationToken.None);

        service.MarkComplete("toy.a");
        service.Current.LastPlayedMissionId.Should().Be("toy.a");

        service.MarkComplete("toy.b");
        service.Current.LastPlayedMissionId.Should().Be("toy.b");
    }

    [Fact]
    public async System.Threading.Tasks.Task MarkLastPlayed_does_not_count_as_completion()
    {
        var (service, _, spec) = BuildService();
        await service.LoadAsync(CancellationToken.None);

        service.MarkLastPlayed("toy.a");
        service.Current.LastPlayedMissionId.Should().Be("toy.a");
        service.Current.CompletedMissionIds.Should().BeEmpty();
        service.IsUnlocked("toy.b", spec).Should().BeFalse();
    }

    [Fact]
    public async System.Threading.Tasks.Task Progress_round_trips_through_save_and_load()
    {
        var (first, vfs, spec) = BuildService();
        await first.LoadAsync(CancellationToken.None);
        first.MarkComplete("toy.a");
        first.MarkComplete("toy.b");
        await first.SaveAsync(CancellationToken.None);

        var second = BuildServiceFor(vfs, spec);
        await second.LoadAsync(CancellationToken.None);

        second.Current.CompletedMissionIds.Should().BeEquivalentTo(new[] { "toy.a", "toy.b" });
        second.Current.LastPlayedMissionId.Should().Be("toy.b");
    }

    [Fact]
    public async System.Threading.Tasks.Task Mismatched_campaign_id_in_save_resets_progress()
    {
        var vfs = new InMemoryVfs();
        var foreignDto = new CampaignProgressDto
        {
            CampaignId = "different_campaign",
            CompletedMissionIds = new List<string> { "other.mission" },
            LastPlayedMissionId = "other.mission",
        };
        var foreignHeader = SaveHeader.Current(
            schemaVersion: SaveSchemas.CampaignProgress,
            TestBuildInfo.Version,
            unixMs: 0);
        var foreignFrame = SaveHeaderSerializer.WriteFrame(foreignHeader, foreignDto, new MemoryPackCodec());
        await vfs.WriteAllBytesAtomicAsync(CampaignProgressService.ProgressPath, foreignFrame, CancellationToken.None);

        var spec = BuildToySpec();
        var service = BuildServiceFor(vfs, spec);
        await service.LoadAsync(CancellationToken.None);

        service.Current.CampaignId.Should().Be(spec.Id);
        service.Current.CompletedMissionIds.Should().BeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task Corrupt_progress_payload_is_replaced_by_empty_state()
    {
        var vfs = new InMemoryVfs();
        await vfs.WriteAllBytesAtomicAsync(
            CampaignProgressService.ProgressPath,
            System.Text.Encoding.UTF8.GetBytes("not a binary frame at all"),
            CancellationToken.None);

        var service = BuildServiceFor(vfs, BuildToySpec());
        await service.LoadAsync(CancellationToken.None);

        service.Current.CompletedMissionIds.Should().BeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task LoadAsync_with_future_schema_version_resets_progress()
    {
        var vfs = new InMemoryVfs();
        var futureHeader = SaveHeader.Current(
            schemaVersion: SaveSchemas.CampaignProgress + 99,
            TestBuildInfo.Version,
            unixMs: 0);
        var futureDto = new CampaignProgressDto
        {
            CampaignId = "toy_campaign",
            CompletedMissionIds = new List<string> { "toy.a" },
            LastPlayedMissionId = "toy.a",
        };
        var futureFrame = SaveHeaderSerializer.WriteFrame(futureHeader, futureDto, new MemoryPackCodec());
        await vfs.WriteAllBytesAtomicAsync(CampaignProgressService.ProgressPath, futureFrame, CancellationToken.None);

        var service = BuildServiceFor(vfs, BuildToySpec());
        await service.LoadAsync(CancellationToken.None);

        service.Current.CompletedMissionIds.Should().BeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task IsUnlocked_throws_for_mission_id_not_in_spec()
    {
        var (service, _, spec) = BuildService();
        await service.LoadAsync(CancellationToken.None);
        var act = () => service.IsUnlocked("unknown.mission", spec);
        act.Should().Throw<KeyNotFoundException>().WithMessage("*unknown.mission*");
    }

    [Fact]
    public async System.Threading.Tasks.Task Changed_fires_with_the_new_progress_value()
    {
        var (service, _, _) = BuildService();
        await service.LoadAsync(CancellationToken.None);
        CampaignProgress? observed = null;
        service.Changed += p => observed = p;

        service.MarkComplete("toy.a");

        observed.Should().NotBeNull();
        observed!.CompletedMissionIds.Should().Contain("toy.a");
        observed.LastPlayedMissionId.Should().Be("toy.a");
    }

    [Fact]
    public async System.Threading.Tasks.Task Saved_frame_carries_current_schema_app_version_and_clock_timestamp()
    {
        var vfs = new InMemoryVfs();
        var spec = BuildToySpec();
        var clock = new FakeClock { Now = 1_750_000_000_000L };
        var service = new CampaignProgressService(
            vfs,
            spec,
            new MemoryPackCodec(),
            clock,
            TestBuildInfo,
            NullLogger<CampaignProgressService>.Instance);

        await service.LoadAsync(CancellationToken.None);

        var blob = vfs.Files[CampaignProgressService.ProgressPath];
        var read = SaveHeaderSerializer.ReadFrame<CampaignProgressDto>(blob, new MemoryPackCodec());

        read.IsOk.Should().BeTrue();
        var (header, body) = read.Unwrap();
        header.Magic.Should().Be(SaveHeader.MagicV1);
        header.SchemaVersion.Should().Be(SaveSchemas.CampaignProgress);
        header.AuthoringVersion.Should().Be(TestBuildInfo.Version);
        header.CreatedAtUnixMs.Should().Be(clock.Now);
        body.CampaignId.Should().Be(spec.Id);
    }
}
