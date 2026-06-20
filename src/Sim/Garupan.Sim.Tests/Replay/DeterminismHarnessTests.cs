using FluentAssertions;
using Garupan.Sim.Replay;
using Xunit;

namespace Garupan.Sim.Tests.Replay;

public sealed class DeterminismHarnessTests
{
    [Fact]
    public void Same_tick_count_produces_the_same_replay_hash()
    {
        var first = DeterminismHarness.ComputeReplayHash(60, ReplayTestKeys.IntegrityKey);
        var second = DeterminismHarness.ComputeReplayHash(60, ReplayTestKeys.IntegrityKey);

        first.Should().Be(
            second,
            "the canonical scenario is deterministic — a hash mismatch is a non-deterministic regression somewhere in the sim");
    }

    [Fact]
    public void Different_tick_counts_produce_different_hashes()
    {
        var ten = DeterminismHarness.ComputeReplayHash(10, ReplayTestKeys.IntegrityKey);
        var hundred = DeterminismHarness.ComputeReplayHash(100, ReplayTestKeys.IntegrityKey);

        ten.Should().NotBe(hundred);
    }

    [Fact]
    public void Hash_format_is_uppercase_hex_sha256()
    {
        var hash = DeterminismHarness.ComputeReplayHash(1, ReplayTestKeys.IntegrityKey);

        hash.Should().HaveLength(64, "SHA-256 hex is 64 characters");
        foreach (var c in hash)
        {
            ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')).Should().BeTrue(
                $"hash should be uppercase hex; saw '{c}'");
        }
    }
}
