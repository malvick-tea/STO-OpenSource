using System.Collections.Generic;
using Opus.Foundation;

namespace Garupan.Content;

/// <summary>
/// A single playable match in a campaign. Pure data — no behaviour. AI scripting hooks
/// (per-school personality + canon beats like RivalDelta's snowstorm) live in the Sim layer
/// and consume the <see cref="ScriptId"/> to look up their behaviour graph.
/// </summary>
/// <param name="Id">Stable identifier — used by save data + URL routing.</param>
/// <param name="TitleKey">Translation key for the mission's display title.</param>
/// <param name="EpisodeReference">"Episode 8" / "Optional Match" — for tooltip / archive use.</param>
/// <param name="Opponent">The school the player faces.</param>
/// <param name="Environment">Coarse map / backdrop tag.</param>
/// <param name="Objective">Win condition.</param>
/// <param name="LoreSummaryKey">Translation key for the 1-2 sentence "what happened" blurb.</param>
/// <param name="BriefingKey">Translation key for the full pre-match briefing copy.</param>
/// <param name="ScriptId">Identifier the Sim layer maps to AI / scripted-beat behaviour.</param>
public sealed record MissionSpec(
    string Id,
    string TitleKey,
    string EpisodeReference,
    OpponentSchool Opponent,
    MissionEnvironment Environment,
    MissionObjective Objective,
    string LoreSummaryKey,
    string BriefingKey,
    string ScriptId)
{
    public static MissionSpec Of(
        string id,
        string titleKey,
        string episodeReference,
        OpponentSchool opponent,
        MissionEnvironment environment,
        MissionObjective objective,
        string loreSummaryKey,
        string briefingKey,
        string scriptId)
    {
        Ensure.NotNullOrEmpty(id);
        Ensure.NotNullOrEmpty(titleKey);
        Ensure.NotNullOrEmpty(episodeReference);
        Ensure.NotNullOrEmpty(loreSummaryKey);
        Ensure.NotNullOrEmpty(briefingKey);
        Ensure.NotNullOrEmpty(scriptId);
        return new MissionSpec(id, titleKey, episodeReference, opponent, environment, objective, loreSummaryKey, briefingKey, scriptId);
    }
}

/// <summary>One node in the campaign graph — references a mission + 2D layout position.</summary>
/// <param name="MissionId">Foreign key to <see cref="MissionSpec.Id"/>.</param>
/// <param name="X">Logical X position in the graph (0..1, screen-resolution-agnostic).</param>
/// <param name="Y">Logical Y position in the graph.</param>
/// <param name="Prerequisites">Mission ids that must be complete before this node unlocks.</param>
public sealed record CampaignNode(
    string MissionId,
    float X,
    float Y,
    IReadOnlyList<string> Prerequisites);

/// <summary>A canon-ordered storyline. Multiple of these will exist (the player commander's arc, the rival commander's
/// prequel arc, a later season, etc.); the player picks which to start from a meta menu in
/// later milestones.</summary>
/// <param name="Id">Stable identifier.</param>
/// <param name="NameKey">Translation key for display name.</param>
/// <param name="ShortDescriptionKey">Translation key for the 1-line subtitle on the picker.</param>
/// <param name="Missions">Mission specs in canonical order.</param>
/// <param name="Nodes">Layout positions — must reference exactly the missions in <see cref="Missions"/>.</param>
public sealed record CampaignSpec(
    string Id,
    string NameKey,
    string ShortDescriptionKey,
    IReadOnlyList<MissionSpec> Missions,
    IReadOnlyList<CampaignNode> Nodes);
