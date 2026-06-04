using System.Net;
using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Xunit;

namespace Garupan.Client.Ui.Tests.Match.Network;

/// <summary>
/// Pins the local test connect target <see cref="NetworkMatchEndpoint"/> hands the
/// lobby by default — loopback on the canonical port, so a dev box running both halves
/// of the stack connects with zero config.
/// </summary>
public sealed class NetworkMatchEndpointTests
{
    [Fact]
    public void Default_endpoint_is_loopback_on_the_canonical_port()
    {
        var endpoint = NetworkMatchEndpoint.Default;

        endpoint.ServerEndpoint.Address.Should().Be(IPAddress.Loopback);
        endpoint.ServerEndpoint.Port.Should().Be(NetworkMatchEndpoint.DefaultPort);
    }

    [Fact]
    public void Default_port_is_the_seven_thousand_seven_seven_seven_shared_with_the_server_console()
    {
        // ServerConsoleOptionsParser.DefaultPort is the same literal — the two halves of
        // the local test stack share the value so an out-of-box dev run needs no flags.
        NetworkMatchEndpoint.DefaultPort.Should().Be(7777);
    }

    [Fact]
    public void A_custom_endpoint_is_carried_verbatim()
    {
        var custom = new IPEndPoint(IPAddress.Parse("10.0.0.5"), 5500);

        var endpoint = new NetworkMatchEndpoint(custom);

        endpoint.ServerEndpoint.Should().BeSameAs(custom);
    }
}
