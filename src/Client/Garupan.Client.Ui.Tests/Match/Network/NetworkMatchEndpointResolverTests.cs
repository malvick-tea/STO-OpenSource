using System.Net;
using FluentAssertions;
using Garupan.Client.Core.Application;
using Garupan.Client.Ui.Match.Network;
using Xunit;

namespace Garupan.Client.Ui.Tests.Match.Network;

/// <summary>
/// Pure-resolution coverage for <see cref="NetworkMatchEndpointResolver"/>. The resolver
/// never throws — every malformed input path returns the loopback default so the lobby
/// always has a dial-able target. DNS lookups against unreachable hostnames are
/// intentionally tested through "loopback" / "localhost" — names that resolve on every
/// supported developer box.
/// </summary>
public sealed class NetworkMatchEndpointResolverTests
{
    [Fact]
    public void Resolve_parses_an_ipv4_literal_verbatim()
    {
        var settings = new MultiplayerSettings { Host = "203.0.113.42", Port = 8888 };

        var endpoint = NetworkMatchEndpointResolver.Resolve(settings);

        endpoint.ServerEndpoint.Address.Should().Be(IPAddress.Parse("203.0.113.42"));
        endpoint.ServerEndpoint.Port.Should().Be(8888);
    }

    [Fact]
    public void Resolve_parses_the_loopback_literal_to_the_loopback_address()
    {
        var settings = new MultiplayerSettings { Host = MultiplayerSettings.LoopbackHost, Port = 7777 };

        var endpoint = NetworkMatchEndpointResolver.Resolve(settings);

        endpoint.ServerEndpoint.Address.Should().Be(IPAddress.Loopback);
        endpoint.ServerEndpoint.Port.Should().Be(7777);
    }

    [Fact]
    public void Resolve_parses_a_dns_name_to_some_concrete_address()
    {
        var settings = new MultiplayerSettings { Host = "localhost", Port = 7777 };

        var endpoint = NetworkMatchEndpointResolver.Resolve(settings);

        endpoint.ServerEndpoint.Address.Should().NotBeNull();
        endpoint.ServerEndpoint.Port.Should().Be(7777);
    }

    [Fact]
    public void Resolve_falls_back_to_default_when_host_is_empty()
    {
        var settings = new MultiplayerSettings { Host = string.Empty, Port = 7777 };

        var endpoint = NetworkMatchEndpointResolver.Resolve(settings);

        endpoint.Should().Be(NetworkMatchEndpoint.Default);
    }

    [Fact]
    public void Resolve_falls_back_to_default_when_host_is_whitespace()
    {
        var settings = new MultiplayerSettings { Host = "   ", Port = 7777 };

        var endpoint = NetworkMatchEndpointResolver.Resolve(settings);

        endpoint.Should().Be(NetworkMatchEndpoint.Default);
    }

    [Fact]
    public void Resolve_falls_back_to_default_when_port_is_zero()
    {
        var settings = new MultiplayerSettings { Host = "127.0.0.1", Port = 0 };

        var endpoint = NetworkMatchEndpointResolver.Resolve(settings);

        endpoint.Should().Be(NetworkMatchEndpoint.Default);
    }

    [Fact]
    public void Resolve_falls_back_to_default_when_port_is_above_legal_range()
    {
        var settings = new MultiplayerSettings { Host = "127.0.0.1", Port = MultiplayerSettings.MaxPort + 1 };

        var endpoint = NetworkMatchEndpointResolver.Resolve(settings);

        endpoint.Should().Be(NetworkMatchEndpoint.Default);
    }

    [Fact]
    public void Resolve_falls_back_to_default_for_an_unresolvable_hostname()
    {
        // A guaranteed-unresolvable label (RFC 6761 reserves ".invalid" for exactly this).
        var settings = new MultiplayerSettings { Host = "match-test.invalid", Port = 7777 };

        var endpoint = NetworkMatchEndpointResolver.Resolve(settings);

        endpoint.Should().Be(NetworkMatchEndpoint.Default);
    }

    [Fact]
    public void Resolve_trims_whitespace_around_the_host_before_parsing()
    {
        var settings = new MultiplayerSettings { Host = "  10.0.0.5  ", Port = 5500 };

        var endpoint = NetworkMatchEndpointResolver.Resolve(settings);

        endpoint.ServerEndpoint.Address.Should().Be(IPAddress.Parse("10.0.0.5"));
        endpoint.ServerEndpoint.Port.Should().Be(5500);
    }
}
