namespace Garupan.Client.Core.Application;

/// <summary>Centralised <see cref="Opus.Persistence.SaveHeader.SchemaVersion"/> constants
/// per game save kind. Bump the matching constant when the on-disk shape of that body
/// changes; the loader gates on equality and routes mismatches through the migration
/// pipeline (planned). Keeping these in one place means a binary search for "where do I
/// bump the version" returns a single file rather than a survey of every service.
/// <para>
/// These are game save-kind versions, so they live in the game — the genre-neutral
/// <c>Opus.Persistence</c> engine owns the generic framing, not the version numbers of
/// the game's specific save bodies.
/// </para></summary>
public static class SaveSchemas
{
    /// <summary><see cref="AppSettings"/> binary frame.
    /// V1: locale + window dims + vsync + master/music/sfx gains.
    /// V2: adds <see cref="InputBindings"/> — the rebindable match keyboard controls.
    /// V3: adds <see cref="MultiplayerSettings"/> — the local test match server
    /// endpoint (host + port).
    /// V4: <see cref="MultiplayerSettings"/> gains the two per-mode
    /// <see cref="MultiplayerEndpointOverride"/> rows (Hungry Battles + Tactical 5v5) so a
    /// tester binds one endpoint per match mode and the lobby dials the matching one for
    /// the clicked card.</summary>
    public const int Settings = 4;

    /// <summary><see cref="CampaignProgress"/> binary frame. V1 carries: campaign id +
    /// completed mission ids + last-played mission id.</summary>
    public const int CampaignProgress = 1;
}
