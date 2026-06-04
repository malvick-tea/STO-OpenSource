using System;
using Garupan.Content;

namespace Garupan.Client.Ui.Tests.Fixtures;

/// <summary>
/// Hand-built <see cref="CampaignSpec"/> instances for screen-layer tests. Kept tiny and
/// in-code so progression tests don't depend on the on-disk <c>sample.csv</c> or its loader.
/// </summary>
internal static class TestCampaign
{
    /// <summary>A three-mission linear chain: m1 → m2 → m3, each gating the next.</summary>
    public static CampaignSpec Linear3()
    {
        var missions = new[]
        {
            Mission("m1"),
            Mission("m2"),
            Mission("m3"),
        };
        var nodes = new[]
        {
            new CampaignNode("m1", 0.10f, 0.5f, Array.Empty<string>()),
            new CampaignNode("m2", 0.40f, 0.5f, new[] { "m1" }),
            new CampaignNode("m3", 0.70f, 0.5f, new[] { "m2" }),
        };
        return new CampaignSpec("test_campaign", "campaign.test.name", "campaign.test.subtitle", missions, nodes);
    }

    /// <summary>Thirteen-mission linear chain: m01 → m02 → … → m13. Used to exercise the
    /// 12-mission arc-block gate (the thirteenth node must stay hidden until all of the
    /// first twelve are cleared).</summary>
    public static CampaignSpec Linear13()
    {
        var missions = new MissionSpec[13];
        var nodes = new CampaignNode[13];
        for (var i = 0; i < 13; i++)
        {
            var id = $"m{i + 1:D2}";
            missions[i] = Mission(id);
            var prereqs = i == 0 ? Array.Empty<string>() : new[] { $"m{i:D2}" };
            nodes[i] = new CampaignNode(id, 0.05f + (0.07f * i), 0.5f, prereqs);
        }

        return new CampaignSpec("test_campaign_long", "campaign.long.name", "campaign.long.subtitle", missions, nodes);
    }

    private static MissionSpec Mission(string id) => MissionSpec.Of(
        id,
        $"campaign.test.{id}.title",
        "Episode 0",
        OpponentSchool.PlayerSchool,
        MissionEnvironment.RuralOpen,
        MissionObjective.Bracket,
        $"campaign.test.{id}.summary",
        $"campaign.test.{id}.briefing",
        $"scripted/{id}");
}
