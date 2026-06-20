using FluentAssertions;
using Xunit;

namespace Garupan.Server.Match.Tests;

public sealed class MatchAdmissionPolicyTests
{
    [Fact]
    public void Allows_players_before_match_is_contested()
    {
        var policy = new MatchAdmissionPolicy(maximumPlayers: 4, allowLateJoin: false);

        policy.Evaluate(currentPlayerCount: 1).Should().Be(MatchAdmissionDecision.Allowed);
    }

    [Fact]
    public void Rejects_players_at_capacity()
    {
        var policy = new MatchAdmissionPolicy(maximumPlayers: 2, allowLateJoin: true);

        policy.Evaluate(currentPlayerCount: 2).Should().Be(MatchAdmissionDecision.CapacityReached);
    }

    [Fact]
    public void Rejects_late_join_after_match_was_contested()
    {
        var policy = new MatchAdmissionPolicy(maximumPlayers: 4, allowLateJoin: false);
        policy.ObservePlayerCount(2);
        policy.ObservePlayerCount(1);

        policy.Evaluate(currentPlayerCount: 1).Should().Be(MatchAdmissionDecision.LateJoinDisabled);
    }

    [Fact]
    public void Allows_late_join_when_explicitly_enabled()
    {
        var policy = new MatchAdmissionPolicy(maximumPlayers: 4, allowLateJoin: true);
        policy.ObservePlayerCount(2);

        policy.Evaluate(currentPlayerCount: 1).Should().Be(MatchAdmissionDecision.Allowed);
    }

    [Fact]
    public void Reset_opens_admission_for_next_match()
    {
        var policy = new MatchAdmissionPolicy(maximumPlayers: 4, allowLateJoin: false);
        policy.ObservePlayerCount(2);

        policy.Reset();

        policy.Evaluate(currentPlayerCount: 1).Should().Be(MatchAdmissionDecision.Allowed);
    }
}
