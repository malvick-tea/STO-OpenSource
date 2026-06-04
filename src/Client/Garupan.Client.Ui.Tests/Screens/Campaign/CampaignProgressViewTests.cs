using FluentAssertions;
using Garupan.Client.Core.Application;
using Garupan.Client.Ui.Screens.Campaign;
using Garupan.Client.Ui.Tests.Fixtures;
using Garupan.Content;
using Xunit;

namespace Garupan.Client.Ui.Tests.Screens.Campaign;

/// <summary>
/// Covers <see cref="CampaignProgressView"/> — the read model that turns a
/// <see cref="CampaignProgress"/> snapshot into per-node hidden / locked / available /
/// complete status. Two campaign fixtures: <see cref="TestCampaign.Linear3"/> for the
/// first-mission gate and <see cref="TestCampaign.Linear13"/> for the 12-mission
/// arc-block gate.
/// </summary>
public sealed class CampaignProgressViewTests
{
    private static readonly CampaignSpec Spec = TestCampaign.Linear3();

    [Fact]
    public void Fresh_profile_shows_first_node_available_and_the_rest_hidden()
    {
        var view = new CampaignProgressView(Spec, CampaignProgress.Empty(Spec.Id));

        view.StatusAt(0).Should().Be(CampaignNodeStatus.Available);
        view.StatusAt(1).Should().Be(CampaignNodeStatus.Hidden);
        view.StatusAt(2).Should().Be(CampaignNodeStatus.Hidden);
    }

    [Fact]
    public void Clearing_the_first_mission_reveals_the_rest_of_the_arc()
    {
        var progress = CampaignProgress.Empty(Spec.Id).WithMissionCompleted("m1");

        var view = new CampaignProgressView(Spec, progress);

        view.StatusAt(0).Should().Be(CampaignNodeStatus.Completed);
        view.StatusAt(1).Should().Be(CampaignNodeStatus.Available);
        view.StatusAt(2).Should().Be(CampaignNodeStatus.Locked, "m3 needs m2 cleared first");
    }

    [Fact]
    public void Clearing_the_whole_chain_unlocks_the_final_mission()
    {
        var progress = CampaignProgress.Empty(Spec.Id)
            .WithMissionCompleted("m1")
            .WithMissionCompleted("m2");

        var view = new CampaignProgressView(Spec, progress);

        view.StatusAt(0).Should().Be(CampaignNodeStatus.Completed);
        view.StatusAt(1).Should().Be(CampaignNodeStatus.Completed);
        view.StatusAt(2).Should().Be(CampaignNodeStatus.Available);
    }

    [Fact]
    public void IsPlayable_is_true_only_for_available_and_completed_nodes()
    {
        var view = new CampaignProgressView(
            Spec, CampaignProgress.Empty(Spec.Id).WithMissionCompleted("m1"));

        view.IsPlayable(0).Should().BeTrue("completed nodes are replayable");
        view.IsPlayable(1).Should().BeTrue("available nodes are playable");

        var fresh = new CampaignProgressView(Spec, CampaignProgress.Empty(Spec.Id));
        fresh.IsPlayable(0).Should().BeTrue("the first node is always at least available");
        fresh.IsPlayable(1).Should().BeFalse("hidden nodes reject clicks");
    }

    [Fact]
    public void IsHidden_only_true_for_concealed_nodes()
    {
        var view = new CampaignProgressView(Spec, CampaignProgress.Empty(Spec.Id));

        view.IsHidden(0).Should().BeFalse();
        view.IsHidden(1).Should().BeTrue();
        view.IsHidden(2).Should().BeTrue();
    }

    [Fact]
    public void Sync_with_an_unchanged_snapshot_does_not_recompute()
    {
        var progress = CampaignProgress.Empty(Spec.Id);
        var view = new CampaignProgressView(Spec, progress);

        view.Sync(progress).Should().BeFalse();
    }

    [Fact]
    public void Sync_with_new_progress_recomputes_the_status_table()
    {
        var view = new CampaignProgressView(Spec, CampaignProgress.Empty(Spec.Id));

        var recomputed = view.Sync(CampaignProgress.Empty(Spec.Id).WithMissionCompleted("m1"));

        recomputed.Should().BeTrue();
        view.StatusAt(0).Should().Be(CampaignNodeStatus.Completed);
        view.StatusAt(1).Should().Be(CampaignNodeStatus.Available);
    }

    [Fact]
    public void Arc_block_gate_hides_the_thirteenth_node_until_the_first_twelve_clear()
    {
        var spec = TestCampaign.Linear13();
        var partial = CampaignProgress.Empty(spec.Id);
        for (var i = 0; i < 11; i++)
        {
            partial = partial.WithMissionCompleted($"m{i + 1:D2}");
        }

        var view = new CampaignProgressView(spec, partial);

        view.StatusAt(11).Should().Be(CampaignNodeStatus.Available, "twelfth node is reachable after the first eleven");
        view.StatusAt(12).Should().Be(CampaignNodeStatus.Hidden, "thirteenth node stays sealed until all twelve clear");
    }

    [Fact]
    public void Arc_block_gate_releases_after_the_twelfth_node_clears()
    {
        var spec = TestCampaign.Linear13();
        var all = CampaignProgress.Empty(spec.Id);
        for (var i = 0; i < 12; i++)
        {
            all = all.WithMissionCompleted($"m{i + 1:D2}");
        }

        var view = new CampaignProgressView(spec, all);

        view.StatusAt(11).Should().Be(CampaignNodeStatus.Completed);
        view.StatusAt(12).Should().Be(CampaignNodeStatus.Available);
    }
}
