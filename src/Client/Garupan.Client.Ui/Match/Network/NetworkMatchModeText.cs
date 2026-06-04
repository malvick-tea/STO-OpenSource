using Garupan.Content;
using Garupan.Sim.Protocol;

namespace Garupan.Client.Ui.Match.Network;

/// <summary>Client-side translation surface for the match-mode kind: maps the
/// <see cref="MatchModeKind"/> a lobby card carries (the <see cref="Opus.Content"/>
/// catalogue enum) onto the <see cref="WelcomeMatchModeKind"/> the server reports on the
/// wire, and renders either as a human-readable label.
/// <para>
/// The label strings stay English — the network match screen is terminal / telemetry
/// chrome, not localised UI, matching the Phase 18 / 20 / 44 precedent.
/// </para></summary>
internal static class NetworkMatchModeText
{
    /// <summary>Maps a lobby card's <see cref="MatchModeKind"/> onto the
    /// <see cref="WelcomeMatchModeKind"/> the server stamps in its welcome frame, so the
    /// screen can compare the mode the player picked against the mode they actually
    /// joined.</summary>
    public static WelcomeMatchModeKind FromContent(MatchModeKind kind) => kind switch
    {
        MatchModeKind.TeamTactical => WelcomeMatchModeKind.TeamTactical,
        _ => WelcomeMatchModeKind.FreeForAll,
    };

    /// <summary>Human-readable label for a wire mode-kind — the local test line-up's
    /// two modes.</summary>
    public static string Label(WelcomeMatchModeKind kind) => kind switch
    {
        WelcomeMatchModeKind.TeamTactical => "TACTICAL 5v5",
        _ => "HUNGRY BATTLES",
    };
}
