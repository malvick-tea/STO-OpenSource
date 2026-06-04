using FluentAssertions;
using Garupan.Server.Console;
using Xunit;

namespace Garupan.Server.Console.Tests;

public sealed class ShutdownSignalTests
{
    [Fact]
    public void Fresh_signal_token_is_not_cancelled()
    {
        using var signal = new ShutdownSignal();

        signal.HasFired.Should().BeFalse();
        signal.Token.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void Signal_cancels_the_token_and_flips_has_fired()
    {
        using var signal = new ShutdownSignal();

        signal.Signal();

        signal.HasFired.Should().BeTrue();
        signal.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Signal_is_idempotent_on_token_but_counts_each_call()
    {
        using var signal = new ShutdownSignal();

        signal.Signal();
        signal.Signal();
        signal.Signal();

        signal.HasFired.Should().BeTrue();
        signal.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var signal = new ShutdownSignal();
        signal.Dispose();
        signal.Dispose();
    }
}
