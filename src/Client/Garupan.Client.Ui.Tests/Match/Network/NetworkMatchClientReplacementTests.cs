using System;
using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Garupan.Net.Session;
using Garupan.Sim.Protocol;
using Opus.Net.Loopback;
using Xunit;

namespace Garupan.Client.Ui.Tests.Match.Network;

/// <summary>
/// Behavioural coverage for <see cref="NetworkMatchClientReplacement.TryReplace"/> —
/// the helper the screen calls when the player presses Enter on a Failed / Disconnected
/// terminal banner. A retryable state swaps in a fresh client (the old one disposed); a
/// live state or a null factory leaves the original untouched.
/// </summary>
public sealed class NetworkMatchClientReplacementTests
{
    [Fact]
    public void Retry_on_disconnected_disposes_the_old_client_and_returns_the_factory_output()
    {
        using var rig = new ReplacementRig();
        rig.OriginalClient.State.Should().Be(NetworkMatchConnectionState.Connecting);
        rig.OriginalClient.Pump(); // surface Connected on the loopback link
        rig.Disconnect();
        rig.OriginalClient.Pump(); // drain the Disconnected event into the session
        rig.OriginalClient.State.Should().Be(NetworkMatchConnectionState.Disconnected);

        var replaced = NetworkMatchClientReplacement.TryReplace(rig.OriginalClient, rig.Factory);

        replaced.Should().NotBeSameAs(rig.OriginalClient, "a retryable state must produce a fresh client");
        rig.OriginalLink.Client.IsOpen.Should().BeFalse("the prior client owned its transport and must close it");
        replaced.State.Should().Be(NetworkMatchConnectionState.Connecting, "a freshly-opened client starts in Connecting");
        rig.ReplacementOpenCount.Should().Be(1);
    }

    [Fact]
    public void Retry_on_failed_disposes_the_old_client_and_returns_a_fresh_one()
    {
        using var rig = new ReplacementRig(failOnFirst: true);
        rig.AdvanceClockPastDeadline();
        rig.OriginalClient.Pump();
        rig.OriginalClient.State.Should().Be(NetworkMatchConnectionState.Failed);

        var replaced = NetworkMatchClientReplacement.TryReplace(rig.OriginalClient, rig.Factory);

        replaced.Should().NotBeSameAs(rig.OriginalClient);
        replaced.State.Should().Be(NetworkMatchConnectionState.Connecting);
        rig.ReplacementOpenCount.Should().Be(1);
    }

    [Fact]
    public void Retry_on_connecting_returns_the_same_client_untouched()
    {
        using var rig = new ReplacementRig();

        var replaced = NetworkMatchClientReplacement.TryReplace(rig.OriginalClient, rig.Factory);

        replaced.Should().BeSameAs(rig.OriginalClient, "a live connecting client must not be torn down by a retry call");
        rig.ReplacementOpenCount.Should().Be(0);
    }

    [Fact]
    public void Retry_with_a_null_factory_returns_the_same_client_untouched()
    {
        using var rig = new ReplacementRig();
        rig.OriginalClient.Pump();
        rig.Disconnect();
        rig.OriginalClient.Pump();
        rig.OriginalClient.State.Should().Be(NetworkMatchConnectionState.Disconnected);

        var replaced = NetworkMatchClientReplacement.TryReplace(rig.OriginalClient, factory: null);

        replaced.Should().BeSameAs(rig.OriginalClient, "no factory = no swap, even on a retryable state");
        rig.ReplacementOpenCount.Should().Be(0);
    }

    /// <summary>Owns a pair of loopback links — one for the initial client, one each for
    /// any factory-minted replacement. Tests drive the lifecycle through the rig so the
    /// factory closure stays a one-line lambda.</summary>
    private sealed class ReplacementRig : IDisposable
    {
        private readonly bool _failOnFirst;
        private TimeSpan _elapsed = TimeSpan.Zero;
        private NetworkMatchClient? _latestReplacement;

        public ReplacementRig(bool failOnFirst = false)
        {
            _failOnFirst = failOnFirst;
            OriginalLink = LoopbackTransportPair.Create();
            OriginalServer = new ServerSession(OriginalLink.Server);
            OriginalClient = new NetworkMatchClient(
                OriginalLink.Client,
                ownsTransport: true,
                elapsedSource: () => _elapsed);
        }

        public LoopbackTransportLink OriginalLink { get; }

        public ServerSession OriginalServer { get; }

        public NetworkMatchClient OriginalClient { get; }

        public int ReplacementOpenCount { get; private set; }

        public Func<NetworkMatchClient> Factory => () =>
        {
            ReplacementOpenCount++;
            var link = LoopbackTransportPair.Create();
            _latestReplacement?.Dispose();
            _latestReplacement = new NetworkMatchClient(link.Client, ownsTransport: true);

            // The link's server side is intentionally orphaned — these tests assert the
            // replacement's lifecycle, not its handshake. Each replacement gets its own
            // fresh transport pair so the original's loopback link state is unaffected.
            link.Server.Dispose();
            return _latestReplacement;
        };

        public void Disconnect() => OriginalLink.Server.Disconnect(OriginalLink.ClientPeerId);

        public void AdvanceClockPastDeadline() => _elapsed = TimeSpan.FromSeconds(60);

        public void Dispose()
        {
            OriginalServer.Dispose();
            OriginalClient.Dispose();
            OriginalLink.Server.Dispose();
            _latestReplacement?.Dispose();
            _ = _failOnFirst;
        }
    }
}
