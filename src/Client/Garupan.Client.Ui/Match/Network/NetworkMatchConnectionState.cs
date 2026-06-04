namespace Garupan.Client.Ui.Match.Network;

/// <summary>
/// Where the client is in the connect → play → drop lifecycle. Drives the lobby and
/// the network match screen's status text + input gating.
/// </summary>
public enum NetworkMatchConnectionState
{
    /// <summary>Transport has been opened; the client has sent (or is sending) Hello but
    /// has not yet received WelcomeAck. <c>SendInput</c> returns false in this phase.</summary>
    Connecting,

    /// <summary>WelcomeAck arrived; <see cref="NetworkMatchClient.LocalNetworkId"/> is
    /// populated; the session can send inputs and receives snapshots.</summary>
    Connected,

    /// <summary>Transport surfaced a Disconnected event (clean close, or dead-peer
    /// timeout). The client cannot send anything past this point.</summary>
    Disconnected,

    /// <summary>The connect attempt ran past its deadline with no WelcomeAck — the server
    /// endpoint is unreachable or misconfigured. Terminal; the client never sends. The
    /// screen surfaces this as an actionable "check the Multiplayer settings" diagnostic
    /// instead of stranding the player in <see cref="Connecting"/> forever.</summary>
    Failed,
}
