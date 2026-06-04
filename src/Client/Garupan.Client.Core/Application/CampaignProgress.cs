using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MemoryPack;

namespace Garupan.Client.Core.Application;

/// <summary>
/// Persisted player progress for one campaign. Records which missions have been
/// completed, plus the last-played mission id for "continue" navigation. Pure data —
/// the unlock-by-prerequisite logic lives in <c>CampaignProgressService</c>.
/// </summary>
/// <remarks>
/// This is game-domain save data: it lives in the game (<c>Garupan.Client.Core</c>),
/// not in the genre-neutral <c>Opus.Persistence</c> engine assembly, which only owns the
/// generic framing (<see cref="Opus.Persistence.SaveHeaderSerializer"/> /
/// <c>FramedBlobStore&lt;T&gt;</c>) the game serialises this record through.
/// </remarks>
/// <param name="CampaignId">Campaign-level identifier (e.g. <c>"sample_campaign"</c>).</param>
/// <param name="CompletedMissionIds">Stable mission ids the player has cleared at
/// least once. Immutable so the service hands instances out safely across threads.</param>
/// <param name="LastPlayedMissionId">Last mission the player launched, completed or
/// not. Drives the "continue" button on the campaign screen. <c>null</c> for a
/// brand-new profile.</param>
public sealed record CampaignProgress(
    string CampaignId,
    ImmutableHashSet<string> CompletedMissionIds,
    string? LastPlayedMissionId)
{
    /// <summary>A fresh, empty progress record for the given campaign.</summary>
    public static CampaignProgress Empty(string campaignId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        return new CampaignProgress(campaignId, ImmutableHashSet<string>.Empty, LastPlayedMissionId: null);
    }

    /// <summary>Returns a new record with <paramref name="missionId"/> added to
    /// <see cref="CompletedMissionIds"/> and marked as the last played. Idempotent —
    /// the same missionId twice returns the same hash-set instance.</summary>
    public CampaignProgress WithMissionCompleted(string missionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(missionId);
        var nextCompleted = CompletedMissionIds.Add(missionId);
        return this with { CompletedMissionIds = nextCompleted, LastPlayedMissionId = missionId };
    }

    /// <summary>Returns a new record with <paramref name="missionId"/> recorded as
    /// last played, without marking it complete. Used for "abandoned mid-match" or
    /// mission-briefing-viewed events.</summary>
    public CampaignProgress WithLastPlayed(string missionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(missionId);
        return this with { LastPlayedMissionId = missionId };
    }
}

/// <summary>Serialisable shape of <see cref="CampaignProgress"/> — uses a plain string
/// list rather than the immutable set, which the loader rehydrates. The
/// <see cref="MemoryPackableAttribute"/> markers source-gen the binary serialisers — pair
/// with <see cref="Opus.Persistence.SaveHeaderSerializer"/> for the on-disk frame.</summary>
[MemoryPackable]
public sealed partial class CampaignProgressDto
{
    public string CampaignId { get; set; } = string.Empty;

    public List<string> CompletedMissionIds { get; set; } = new();

    public string? LastPlayedMissionId { get; set; }

    public static CampaignProgressDto From(CampaignProgress progress) => new()
    {
        CampaignId = progress.CampaignId,
        CompletedMissionIds = new List<string>(progress.CompletedMissionIds),
        LastPlayedMissionId = progress.LastPlayedMissionId,
    };

    public CampaignProgress ToRecord() => new(
        CampaignId,
        CompletedMissionIds.ToImmutableHashSet(),
        LastPlayedMissionId);
}
