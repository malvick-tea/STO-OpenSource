using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Opus.Net.Udp.Transport;

namespace Garupan.Client.Ui.Match.Network;

/// <summary>
/// Builds a runtime <see cref="NetworkMatchClient"/> backed by a real
/// <see cref="UdpClientTransport"/>. Tests inject their own transport via the
/// <see cref="NetworkMatchClient"/> ctor instead of going through this factory.
/// </summary>
/// <remarks>
/// Single responsibility — turn a <see cref="NetworkMatchEndpoint"/> into a live client.
/// Pulled out of <c>LobbyScreen</c> so the screen stays render-and-input only and the
/// transport-creation path is testable separately from the screen.
/// </remarks>
public sealed class NetworkMatchClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly UdpTransportOptions _transportOptions;

    public NetworkMatchClientFactory(
        ILoggerFactory? loggerFactory = null,
        UdpTransportOptions? transportOptions = null)
    {
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _transportOptions = transportOptions ?? UdpTransportOptions.Default;
    }

    /// <summary>Opens a UDP client transport against <paramref name="endpoint"/> and
    /// returns a <see cref="NetworkMatchClient"/> that owns it. The client starts in the
    /// Connecting state — the first <see cref="NetworkMatchClient.Pump"/> from the game
    /// tick may surface the Welcome that flips it to Connected.</summary>
    public NetworkMatchClient Create(NetworkMatchEndpoint endpoint)
    {
        System.ArgumentNullException.ThrowIfNull(endpoint);
        var transport = new UdpClientTransport(
            name: "sto-client",
            serverEndpoint: endpoint.ServerEndpoint,
            options: _transportOptions,
            logger: _loggerFactory.CreateLogger<UdpClientTransport>());
        return new NetworkMatchClient(
            transport,
            ownsTransport: true,
            logger: _loggerFactory.CreateLogger<NetworkMatchClient>());
    }
}
