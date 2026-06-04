using System.Collections.Generic;
using System.Numerics;
using Garupan.Sim.Components;

namespace Garupan.Server.Match.Outcome;

/// <summary>
/// Watches a match's roster and decides when it is over. <see cref="MatchHost"/> feeds it
/// the current participants each tick; it evaluates the configured
/// <see cref="MatchOutcomeRule"/> and latches the first decided <see cref="MatchOutcome"/>
/// it reaches — so a straggler knocked out after the match is already won cannot re-open
/// or re-decide a finished match.
/// <para>
/// Pure with respect to the ECS: it consumes a <see cref="MatchParticipant"/> list, not
/// the world, so the rule arithmetic is headless-testable and allocation-free on the
/// per-tick path. A match needs at least two contenders before any outcome is possible —
/// a lone-tank roster is always <see cref="MatchOutcomeKind.InProgress"/>, which keeps a
/// one-player lobby from instantly declaring itself won.
/// </para>
/// </summary>
public sealed class MatchOutcomeTracker
{
    /// <summary>A match cannot be decided until it has this many contenders — two tanks
    /// for <see cref="MatchOutcomeRule.LastTankStanding"/>, two teams for
    /// <see cref="MatchOutcomeRule.LastTeamStanding"/>.</summary>
    private const int MinimumContenders = 2;

    private readonly MatchOutcomeRule _rule;

    public MatchOutcomeTracker(MatchOutcomeRule rule)
    {
        _rule = rule;
    }

    /// <summary>The latched outcome — <see cref="MatchOutcome.InProgress"/> until the
    /// match is decided, then frozen for the rest of the match.</summary>
    public MatchOutcome Current { get; private set; } = MatchOutcome.InProgress;

    /// <summary>Resets the tracker to <see cref="MatchOutcome.InProgress"/> so the next
    /// match on the same host process starts clean. The configured rule is preserved —
    /// only the latched verdict is cleared.</summary>
    public void Reset() => Current = MatchOutcome.InProgress;

    /// <summary>Re-evaluates the configured rule against <paramref name="participants"/>.
    /// A no-op once <see cref="Current"/> is decided — the outcome is final. Returns the
    /// (possibly newly latched) current outcome for the caller's convenience.</summary>
    public MatchOutcome Update(IReadOnlyList<MatchParticipant> participants)
    {
        if (Current.IsDecided)
        {
            return Current;
        }

        var evaluated = _rule switch
        {
            MatchOutcomeRule.LastTeamStanding => EvaluateLastTeamStanding(participants),
            _ => EvaluateLastTankStanding(participants),
        };

        if (evaluated.IsDecided)
        {
            Current = evaluated;
        }

        return Current;
    }

    private static MatchOutcome EvaluateLastTankStanding(IReadOnlyList<MatchParticipant> participants)
    {
        if (participants.Count < MinimumContenders)
        {
            return MatchOutcome.InProgress;
        }

        var aliveCount = 0;
        var lastAliveNetworkId = 0u;
        foreach (var participant in participants)
        {
            if (participant.IsKnockedOut)
            {
                continue;
            }

            aliveCount++;
            lastAliveNetworkId = participant.NetworkId;
        }

        return aliveCount switch
        {
            1 => MatchOutcome.TankWinner(lastAliveNetworkId),
            0 => MatchOutcome.Draw,
            _ => MatchOutcome.InProgress,
        };
    }

    private static MatchOutcome EvaluateLastTeamStanding(IReadOnlyList<MatchParticipant> participants)
    {
        if (participants.Count < MinimumContenders)
        {
            return MatchOutcome.InProgress;
        }

        // Team is a 3-value byte enum — a bit per team, set in an int, is enough to count
        // distinct teams without a per-tick HashSet allocation.
        var contestingMask = 0;
        var survivingMask = 0;
        foreach (var participant in participants)
        {
            var teamBit = 1 << (int)participant.Team;
            contestingMask |= teamBit;
            if (!participant.IsKnockedOut)
            {
                survivingMask |= teamBit;
            }
        }

        if (BitOperations.PopCount((uint)contestingMask) < MinimumContenders)
        {
            // A single-team roster can never be decided by team elimination.
            return MatchOutcome.InProgress;
        }

        return BitOperations.PopCount((uint)survivingMask) switch
        {
            1 => MatchOutcome.TeamWinner((Team)BitOperations.TrailingZeroCount(survivingMask)),
            0 => MatchOutcome.Draw,
            _ => MatchOutcome.InProgress,
        };
    }
}
