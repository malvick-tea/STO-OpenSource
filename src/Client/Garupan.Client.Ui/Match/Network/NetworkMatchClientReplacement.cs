using System;

namespace Garupan.Client.Ui.Match.Network;

/// <summary>
/// Pure replacement helper: gates the "swap the current <see cref="NetworkMatchClient"/>
/// for a freshly-opened one" path the retry affordance triggers. Disposes the previous
/// client + calls <paramref name="factory"/> to mint a replacement when the current
/// state is a retryable terminal one; otherwise reports the current client unchanged.
/// </summary>
/// <remarks>
/// Sits between <see cref="NetworkMatchScreen"/> (which owns the field) and the lobby's
/// factory closure (which knows how to recreate the client) so the swap logic is
/// unit-testable headless — the screen's reach into transport / session machinery is
/// confined to this one helper, the rest of the screen stays composition only.
/// </remarks>
public static class NetworkMatchClientReplacement
{
    /// <summary>Attempts to replace <paramref name="current"/> with a fresh client from
    /// <paramref name="factory"/>. Replaces only when the current state is a retryable
    /// terminal state (Failed / Disconnected); a Connecting or Connected client is not
    /// blown away. The previous client is disposed on a successful replace.</summary>
    /// <returns>The client the screen should hold from this point on — either the
    /// fresh one (replaced) or the original (untouched).</returns>
    public static NetworkMatchClient TryReplace(
        NetworkMatchClient current,
        Func<NetworkMatchClient>? factory)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (factory is null || !NetworkMatchTerminalState.IsRetryable(current.State))
        {
            return current;
        }

        current.Dispose();
        return factory();
    }
}
