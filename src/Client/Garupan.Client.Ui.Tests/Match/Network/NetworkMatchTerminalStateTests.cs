using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Xunit;

namespace Garupan.Client.Ui.Tests.Match.Network;

/// <summary>
/// Coverage for the pure predicate <see cref="NetworkMatchTerminalState.IsRetryable"/> —
/// the gate the screen reads when deciding whether Enter on a terminal banner should
/// open a fresh client.
/// </summary>
public sealed class NetworkMatchTerminalStateTests
{
    [Theory]
    [InlineData(NetworkMatchConnectionState.Failed)]
    [InlineData(NetworkMatchConnectionState.Disconnected)]
    public void Terminal_failure_states_are_retryable(NetworkMatchConnectionState state)
    {
        NetworkMatchTerminalState.IsRetryable(state).Should().BeTrue();
    }

    [Theory]
    [InlineData(NetworkMatchConnectionState.Connecting)]
    [InlineData(NetworkMatchConnectionState.Connected)]
    public void Live_states_are_not_retryable(NetworkMatchConnectionState state)
    {
        NetworkMatchTerminalState.IsRetryable(state).Should().BeFalse(
            "retry only makes sense on terminal failure states — a live connection must not be torn down by Enter");
    }
}
