namespace Garupan.Client.Ui.Match.Network;

/// <summary>
/// Pure classifier over <see cref="NetworkMatchConnectionState"/>: a terminal state is one
/// the screen holds until the player acts on it, and a retryable terminal state is one the
/// player can recover from in place (re-open a fresh client against the same endpoint)
/// instead of backing out to the lobby and re-clicking the card.
/// </summary>
/// <remarks>
/// Splits the predicate out of the screen so it is unit-testable headless and so a future
/// state (e.g. "kicked", "version mismatch", "queued") can plug in without a screen-level
/// edit. Both Failed (the connect deadline elapsed) and Disconnected (an established
/// session dropped) are retryable for the local test — the dev server may have been
/// restarted between the failure and the retry, and the network may have just blipped.
/// </remarks>
public static class NetworkMatchTerminalState
{
    /// <summary>True when <paramref name="state"/> can be recovered from by replacing the
    /// client. Failed and Disconnected both qualify — the player chose this match, the
    /// reason it ended is transient or recoverable, and a retry costs only one fresh
    /// transport open.</summary>
    public static bool IsRetryable(NetworkMatchConnectionState state) =>
        state is NetworkMatchConnectionState.Failed or NetworkMatchConnectionState.Disconnected;
}
