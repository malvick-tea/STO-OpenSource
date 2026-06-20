using System;
using System.Threading;
using System.Threading.Tasks;
using Garupan.Client.Core.Application;
using Garupan.Content;
using Microsoft.Extensions.Logging;
using Opus.Engine.Pal.Filesystem;
using Opus.Foundation;
using Opus.Persistence;

namespace Garupan.Client.Core.Services;

/// <summary>
/// Owns the in-memory <see cref="CampaignProgress"/> snapshot, loads it from
/// <c>user://progress.gsav</c> via <see cref="FramedBlobStore{TBody}"/>, persists
/// changes back atomically. Mirrors the <see cref="SettingsService"/> shape — single
/// Current snapshot, copy-with mutations fire <see cref="Changed"/>, save is
/// fire-and-forget.
/// </summary>
/// <remarks>
/// <para>
/// Unlock semantics: <see cref="IsUnlocked"/> consults the supplied
/// <see cref="CampaignSpec"/>'s <see cref="CampaignNode.Prerequisites"/> against the
/// loaded <see cref="CampaignProgress.CompletedMissionIds"/>. Missions with no
/// prerequisites are always unlocked. The first mission in the canon the player commander arc has no
/// prereqs, so a fresh profile sees exactly one playable node — Prefectural.
/// </para>
/// <para>
/// Cross-campaign safety: a loaded progress whose <c>CampaignId</c> doesn't match the
/// active campaign resets to <see cref="CampaignProgress.Empty"/> — the player
/// switching campaigns doesn't carry stale progress across.
/// </para>
/// </remarks>
public sealed class CampaignProgressService
{
    /// <summary>VFS path of the persisted binary frame. The <c>.gsav</c> extension
    /// disambiguates from any legacy <c>progress.json</c> a stray dev box may still
    /// carry; nothing reads the JSON path any more.</summary>
    public const string ProgressPath = "user://progress.gsav";

    private readonly FramedBlobStore<CampaignProgressDto> _store;
    private readonly ILogger<CampaignProgressService> _logger;
    private readonly string _campaignId;
    private CampaignProgress _current;

    public CampaignProgressService(
        IVfs vfs,
        CampaignSpec campaign,
        IBinaryCodec codec,
        IClock clock,
        BuildInfo buildInfo,
        ISaveIntegrityKeyProvider integrityKeyProvider,
        ILogger<CampaignProgressService> logger)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _campaignId = campaign.Id;
        _current = CampaignProgress.Empty(_campaignId);
        _store = new FramedBlobStore<CampaignProgressDto>(
            vfs, codec, clock, buildInfo, integrityKeyProvider,
            ProgressPath, SaveSchemas.CampaignProgress, "progress", logger);
    }

    public event Action<CampaignProgress>? Changed;

    public CampaignProgress Current => _current;

    public async Task LoadAsync(CancellationToken ct)
    {
        var outcome = await _store.TryLoadAsync(ct).ConfigureAwait(false);
        if (outcome.IsLoaded)
        {
            var loaded = outcome.Body!.ToRecord();
            if (!string.Equals(loaded.CampaignId, _campaignId, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Progress file is for campaign '{Other}' but current campaign is '{This}'; resetting.",
                    loaded.CampaignId, _campaignId);
                _current = CampaignProgress.Empty(_campaignId);
                return;
            }

            _current = loaded;
            _logger.LogInformation(
                "Progress loaded: {Completed} missions cleared, last={Last}",
                _current.CompletedMissionIds.Count,
                _current.LastPlayedMissionId ?? "(none)");
            return;
        }

        _current = CampaignProgress.Empty(_campaignId);
        if (outcome.Status == FramedLoadStatus.NoFile)
        {
            await _store.SaveAsync(CampaignProgressDto.From(_current), ct).ConfigureAwait(false);
        }
    }

    public Task SaveAsync(CancellationToken ct) =>
        _store.SaveAsync(CampaignProgressDto.From(_current), ct);

    /// <summary>Whether the mission identified by <paramref name="missionId"/> is
    /// unlocked under the current progress. Missions with no prerequisites are
    /// always unlocked. Throws <see cref="KeyNotFoundException"/> if the mission id
    /// has no matching node in <paramref name="spec"/>.</summary>
    public bool IsUnlocked(string missionId, CampaignSpec spec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(missionId);
        ArgumentNullException.ThrowIfNull(spec);

        foreach (var node in spec.Nodes)
        {
            if (string.Equals(node.MissionId, missionId, StringComparison.Ordinal))
            {
                return CampaignUnlock.IsNodeUnlocked(node, _current.CompletedMissionIds);
            }
        }

        throw new KeyNotFoundException($"Mission '{missionId}' has no node in campaign '{spec.Id}'.");
    }

    /// <summary>Marks the mission as completed, updates Last-Played, fires
    /// <see cref="Changed"/>, kicks off an async save. Idempotent — a re-complete is
    /// a Last-Played refresh without redundant work.</summary>
    public void MarkComplete(string missionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(missionId);
        var next = _current.WithMissionCompleted(missionId);
        ApplyMutation(next);
    }

    /// <summary>Records the mission as last-played without marking complete. Used
    /// when the player views the briefing or starts but abandons a match.</summary>
    public void MarkLastPlayed(string missionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(missionId);
        var next = _current.WithLastPlayed(missionId);
        ApplyMutation(next);
    }

    /// <summary>Applies a mutation, fires <see cref="Changed"/>, and starts a
    /// tracked save. The returned <see cref="Task"/> completes when the framed
    /// blob is durable on disk (or when the save has logged its failure).
    /// Shutdown paths should await this so a fast exit cannot truncate the
    /// in-flight write.</summary>
    public Task ApplyMutation(CampaignProgress next, CancellationToken ct = default)
    {
        if (next == _current)
        {
            return Task.CompletedTask;
        }

        _current = next;
        Changed?.Invoke(next);
        return _store.SaveAsync(CampaignProgressDto.From(_current), ct);
    }
}
