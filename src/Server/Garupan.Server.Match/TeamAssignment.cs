using Garupan.Server.Match.Outcome;
using Garupan.Sim.Components;

namespace Garupan.Server.Match;

/// <summary>
/// Pure rules for seating the next peer joining a match — the <see cref="Team"/> it is
/// placed on (<see cref="NextTeam"/>) and whether it holds the commander role
/// (<see cref="IsCommander"/>).
/// <para>
/// A free-for-all match (<see cref="MatchOutcomeRule.LastTankStanding"/>) does not read
/// team affiliation — every contender is its own side — so every peer takes the
/// configured free-for-all team. A team match (<see cref="MatchOutcomeRule.LastTeamStanding"/>)
/// balances peers across the two armored combat� teams as they arrive: the next peer fills
/// whichever of <see cref="Team.PlayerSchool"/> / <see cref="Team.OpponentSchool"/>
/// currently has fewer tanks, and a tie fills PlayerSchool first.
/// </para>
/// <para>
/// Pure + headless — <see cref="MatchHost"/> supplies the live per-team counts, so the
/// arithmetic is unit-testable without a world or a transport.
/// </para>
/// </summary>
public static class TeamAssignment
{
    /// <summary>Resolves the team for the next peer joining the match.</summary>
    /// <param name="rule">The match's outcome rule — decides free-for-all vs team match.</param>
    /// <param name="freeForAllTeam">The team every peer takes in a free-for-all match.</param>
    /// <param name="playerSchoolCount">Peers already seated on <see cref="Team.PlayerSchool"/>.</param>
    /// <param name="opponentSchoolCount">Peers already seated on <see cref="Team.OpponentSchool"/>.</param>
    public static Team NextTeam(
        MatchOutcomeRule rule,
        Team freeForAllTeam,
        int playerSchoolCount,
        int opponentSchoolCount)
    {
        if (rule != MatchOutcomeRule.LastTeamStanding)
        {
            return freeForAllTeam;
        }

        return opponentSchoolCount < playerSchoolCount
            ? Team.OpponentSchool
            : Team.PlayerSchool;
    }

    /// <summary>Resolves whether the next peer joining the match holds the commander
    /// role — the first peer seated on a team in a team match commands it. A free-for-all
    /// match has no teams and therefore no commander, so this is always false there.</summary>
    /// <param name="rule">The match's outcome rule — only a team match has commanders.</param>
    /// <param name="assignedTeam">The team <see cref="NextTeam"/> placed this peer on.</param>
    /// <param name="playerSchoolCount">Peers already seated on <see cref="Team.PlayerSchool"/>.</param>
    /// <param name="opponentSchoolCount">Peers already seated on <see cref="Team.OpponentSchool"/>.</param>
    public static bool IsCommander(
        MatchOutcomeRule rule,
        Team assignedTeam,
        int playerSchoolCount,
        int opponentSchoolCount)
    {
        if (rule != MatchOutcomeRule.LastTeamStanding)
        {
            return false;
        }

        var seatedOnAssignedTeam = assignedTeam == Team.OpponentSchool
            ? opponentSchoolCount
            : playerSchoolCount;
        return seatedOnAssignedTeam == 0;
    }
}
