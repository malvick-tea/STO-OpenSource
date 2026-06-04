using System;
using System.Net;
using Garupan.Client.Core.Application;
using Garupan.Sim.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Garupan.Client.Ui.Match.Network;

/// <summary>
/// Pure resolver: turns the player-configured <see cref="MultiplayerSettings"/> into a
/// concrete <see cref="NetworkMatchEndpoint"/> the lobby hands to
/// <see cref="NetworkMatchClientFactory"/>, picking the per-mode override when the
/// player clicked a card that has one configured.
/// </summary>
/// <remarks>
/// <para>
/// The settings layer keeps the host as a free-form string — an IPv4 literal, an IPv6
/// literal in brackets, or a DNS name — so the player can paste any of the forms a
/// local test tester is realistically given. The resolver picks IP-parse first
/// (a numeric literal must always win over a DNS lookup of the same string), then DNS,
/// and falls back to loopback on either a port-out-of-range value or a DNS failure. The
/// fallback is logged at <see cref="LogLevel.Warning"/> so a misconfigured tester gets a
/// diagnostic without crashing the lobby; the alpha goal is "the player always reaches a
/// match", not "the player faces an exception".
/// </para>
/// <para>
/// <b>Per-mode override:</b> when <see cref="Resolve(MultiplayerSettings, WelcomeMatchModeKind, ILogger)"/>
/// is called with a mode kind, the resolver first checks the corresponding
/// <see cref="MultiplayerEndpointOverride"/> on the settings — if configured, it wins.
/// An unconfigured override falls through to the default Host/Port. The mode-less
/// overload exists for callers that don't yet know the mode (legacy paths, tests).
/// </para>
/// </remarks>
public static class NetworkMatchEndpointResolver
{
    /// <summary>Resolves <paramref name="settings"/> to an endpoint using the default
    /// Host/Port, ignoring per-mode overrides. Useful for legacy / no-mode call sites.</summary>
    public static NetworkMatchEndpoint Resolve(
        MultiplayerSettings settings,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ResolveHostPort(settings.Host, settings.Port, logger ?? NullLogger.Instance);
    }

    /// <summary>Resolves <paramref name="settings"/> to an endpoint, preferring the
    /// per-mode override matching <paramref name="modeKind"/>. A configured override
    /// (host populated + port in range) wins over the default Host/Port; anything else
    /// falls through to the default.</summary>
    public static NetworkMatchEndpoint Resolve(
        MultiplayerSettings settings,
        WelcomeMatchModeKind modeKind,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var log = logger ?? NullLogger.Instance;
        var overrideForMode = SelectOverride(settings, modeKind);

        if (overrideForMode.IsConfigured)
        {
            return ResolveHostPort(overrideForMode.Host, overrideForMode.Port, log);
        }

        return ResolveHostPort(settings.Host, settings.Port, log);
    }

    /// <summary>Picks the per-mode override row out of <paramref name="settings"/> for
    /// the given mode kind. An unhandled kind returns <see cref="MultiplayerEndpointOverride.None"/>
    /// — defensive, the resolver then falls back to the default endpoint.</summary>
    private static MultiplayerEndpointOverride SelectOverride(
        MultiplayerSettings settings,
        WelcomeMatchModeKind modeKind) => modeKind switch
    {
        WelcomeMatchModeKind.FreeForAll => settings.HungryBattles,
        WelcomeMatchModeKind.TeamTactical => settings.Tactical,
        _ => MultiplayerEndpointOverride.None,
    };

    private static NetworkMatchEndpoint ResolveHostPort(string? host, int port, ILogger log)
    {
        if (port < MultiplayerSettings.MinPort || port > MultiplayerSettings.MaxPort)
        {
            log.LogWarning(
                "Multiplayer port {Port} is out of the legal {Min}–{Max} range; falling back to loopback default.",
                port,
                MultiplayerSettings.MinPort,
                MultiplayerSettings.MaxPort);
            return NetworkMatchEndpoint.Default;
        }

        var trimmedHost = host?.Trim();
        if (string.IsNullOrEmpty(trimmedHost))
        {
            log.LogWarning("Multiplayer host is empty; falling back to loopback default.");
            return NetworkMatchEndpoint.Default;
        }

        if (IPAddress.TryParse(trimmedHost, out var parsed))
        {
            return new NetworkMatchEndpoint(new IPEndPoint(parsed, port));
        }

        try
        {
            var addresses = Dns.GetHostAddresses(trimmedHost);
            if (addresses.Length > 0)
            {
                return new NetworkMatchEndpoint(new IPEndPoint(addresses[0], port));
            }

            log.LogWarning(
                "DNS lookup for multiplayer host {Host} returned no addresses; falling back to loopback default.",
                trimmedHost);
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or ArgumentException)
        {
            log.LogWarning(
                ex,
                "DNS lookup for multiplayer host {Host} failed; falling back to loopback default.",
                trimmedHost);
        }

        return NetworkMatchEndpoint.Default;
    }
}
