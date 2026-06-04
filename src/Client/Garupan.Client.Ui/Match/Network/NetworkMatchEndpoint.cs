using System.Net;

namespace Garupan.Client.Ui.Match.Network;

/// <summary>
/// Resolved target a <see cref="NetworkMatchClient"/> dials when the player commits to a
/// match from the lobby. Stays a plain record so settings persistence (later phase) and
/// CLI / config-file injection can both populate it without going through DI plumbing.
/// </summary>
/// <param name="ServerEndpoint">The UDP <see cref="IPEndPoint"/> the client opens its
/// session against. Defaults to <c>127.0.0.1:7777</c> — matches
/// <c>ServerConsoleOptionsParser.DefaultPort</c> so a dev box running both halves of the
/// local test stack connects with zero config.</param>
public sealed record NetworkMatchEndpoint(IPEndPoint ServerEndpoint)
{
    /// <summary>Default local test endpoint — loopback on the canonical port. Runtime
    /// deployments override this through the settings layer (Phase 38+).</summary>
    public static readonly NetworkMatchEndpoint Default =
        new(new IPEndPoint(IPAddress.Loopback, DefaultPort));

    /// <summary>Canonical UDP port for the Garupan match server. Mirrors
    /// <c>Garupan.Server.Console.ServerConsoleOptionsParser.DefaultPort</c> — both ends
    /// of the local test stack default to this so an out-of-box dev run just works.</summary>
    public const int DefaultPort = 7777;
}
