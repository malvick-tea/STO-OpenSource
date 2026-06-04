using Arch.Core;
using Garupan.Sim.Components;
using SimWorld = Garupan.Sim.World;

namespace Garupan.Client.Ui.Match;

/// <summary>
/// Reads the world after each tick and decides whether the match has tipped over from
/// <see cref="MatchOutcome.InProgress"/> into Victory / Defeat. Counting goes by
/// <see cref="TeamTag"/> + <see cref="Hull"/> excluding knocked-out tanks.
///
/// Stays separate from <see cref="MatchSession"/> so per-mission victory conditions
/// (flag-capture, breakthrough, survive-N-minutes from <see cref="MissionObjective"/>)
/// can plug in as alternative trackers without touching the session shell.
/// </summary>
public sealed class MatchOutcomeTracker
{
    public MatchOutcome Outcome { get; private set; } = MatchOutcome.InProgress;

    public int AlivePlayers { get; private set; }

    public int AliveOpponents { get; private set; }

    public void Update(SimWorld world)
    {
        var players = 0;
        var opponents = 0;

        var query = new QueryDescription().WithAll<TeamTag, Hull>().WithNone<KnockedOut>();
        world.Raw.Query(in query, (ref TeamTag team, ref Hull _) =>
        {
            switch (team.Team)
            {
                case Team.PlayerSchool: players++; break;
                case Team.OpponentSchool: opponents++; break;
            }
        });

        AlivePlayers = players;
        AliveOpponents = opponents;

        if (Outcome != MatchOutcome.InProgress)
        {
            return;
        }

        if (players == 0)
        {
            Outcome = MatchOutcome.Defeat;
        }
        else if (opponents == 0)
        {
            Outcome = MatchOutcome.Victory;
        }
    }

    public void Seed(int players, int opponents)
    {
        AlivePlayers = players;
        AliveOpponents = opponents;
    }
}
