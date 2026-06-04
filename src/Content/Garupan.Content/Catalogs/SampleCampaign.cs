namespace Garupan.Content;

/// <summary>
/// The default single-player campaign — a fixed sequence of story matches that the
/// rest of the content and sim systems are built to serve.
/// </summary>
/// <remarks>
/// <para>
/// Mission graph data (titles / opponents / objectives / node coordinates /
/// prerequisites) lives in <c>data/campaigns/sample.csv</c> per ADR-0030 and is loaded
/// via <see cref="Load"/>. Story copy lives in <c>localization/*.csv</c> under
/// <c>campaign.sample.*</c> keys per <see cref="MissionSpec.TitleKey"/> /
/// <see cref="MissionSpec.BriefingKey"/> / <see cref="MissionSpec.LoreSummaryKey"/>.
/// </para>
/// <para>
/// Per-mission scripted beats (weather, special rules, scripted opponents) hang off
/// each <see cref="MissionSpec.ScriptId"/> when match-time scripted behaviour is wired.
/// </para>
/// </remarks>
public static class SampleCampaign
{
    /// <summary>Campaign-level identifier. Stable across saves / replays.</summary>
    public const string Id = "sample_campaign";

    /// <summary>Translation key for the campaign's display name.</summary>
    public const string NameKey = "campaign.sample.name";

    /// <summary>Translation key for the campaign-picker subtitle line.</summary>
    public const string ShortDescriptionKey = "campaign.sample.subtitle";

    /// <summary>Loads the campaign from a CSV. The file is bundled next to
    /// the host exe at boot time; callers resolve the path via <c>IVfs.Realize</c>
    /// (typically <c>res://campaigns/sample.csv</c>).</summary>
    public static CampaignSpec Load(string csvPath) =>
        CampaignSpecCsv.LoadFile(csvPath, Id, NameKey, ShortDescriptionKey);
}
