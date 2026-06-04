using System;
using System.Collections.Generic;
using System.Numerics;
using Garupan.Sim.Components;

namespace Garupan.Server.Match;

/// <summary>Pure arithmetic the host needs around peer spawning — where the Nth peer on
/// a given team stands, what hull yaw it begins with, and how many peers already sit on
/// each armored combat� team. Extracted so <see cref="MatchHost"/> stays under the senior
/// 400-line cap and so the maths is unit-testable without touching the transport.</summary>
internal static class MatchHostSpawnPlanner
{
    /// <summary>Default distance (metres) between the PlayerSchool and OpponentSchool
    /// spawn anchors when <see cref="MatchHostOptions.OpponentSpawnAnchor"/> is not
    /// explicitly configured. ~200 m leaves tank crews room to manoeuvre before contact —
    /// inside a 5v5 it is roughly the canonical armored combat� engagement-opening distance.</summary>
    public const float DefaultTeamSeparationMeters = 200f;

    /// <summary>Spawn coordinates for the peer at slot <paramref name="teamSlot"/> on
    /// <paramref name="team"/>: that team's anchor plus N strides in +X.
    /// <list type="bullet">
    /// <item><description>PlayerSchool: anchor = <see cref="MatchHostOptions.SpawnAnchor"/>.</description></item>
    /// <item><description>OpponentSchool: anchor = <see cref="MatchHostOptions.OpponentSpawnAnchor"/>
    /// when set, else <c>SpawnAnchor + (DefaultTeamSeparationMeters, 0)</c>.</description></item>
    /// </list>
    /// A free-for-all match never produces an OpponentSchool peer through
    /// <see cref="TeamAssignment.NextTeam"/>, so this branch is only reached in a team
    /// match — but the default is still well-defined, which keeps tests robust.</summary>
    public static Vector2 ComputeSpawn(MatchHostOptions options, Team team, int teamSlot) =>
        AnchorFor(options, team) + new Vector2(teamSlot * options.SpawnSpacingMeters, 0f);

    /// <summary>Initial hull yaw for a tank spawning on <paramref name="team"/>. The two
    /// teams face each other across the X-axis so a Tactical-5v5 round opens with both
    /// gun barrels pointed at the opposing line: PlayerSchool yaw 0 (faces +X), OpponentSchool
    /// yaw π (faces -X). Free-for-all spawns all share yaw 0 — the rule doesn't read team
    /// affiliation, so a uniform facing is fine.</summary>
    public static float ComputeSpawnYaw(Team team) =>
        team == Team.OpponentSchool ? MathF.PI : 0f;

    /// <summary>Tallies how many connected peers currently sit on each armored combat� team —
    /// the live counts <see cref="TeamAssignment"/> balances the next joiner against,
    /// and the team-slot the spawn planner reads for the new peer's offset.
    /// Allocation-free: walks an enumerable, returns a tuple of int counts.</summary>
    public static (int PlayerSchool, int OpponentSchool) CountTeams(
        IEnumerable<ConnectedPlayer> players)
    {
        var playerSchool = 0;
        var opponentSchool = 0;
        foreach (var player in players)
        {
            if (player.Team == Team.OpponentSchool)
            {
                opponentSchool++;
            }
            else if (player.Team == Team.PlayerSchool)
            {
                playerSchool++;
            }
        }

        return (playerSchool, opponentSchool);
    }

    /// <summary>Picks the spawn anchor for the team's first peer. PlayerSchool always
    /// uses <see cref="MatchHostOptions.SpawnAnchor"/>; OpponentSchool prefers the
    /// configured override, else applies the default team-separation offset on +X.</summary>
    private static Vector2 AnchorFor(MatchHostOptions options, Team team)
    {
        if (team != Team.OpponentSchool)
        {
            return options.SpawnAnchor;
        }

        return options.OpponentSpawnAnchor
            ?? options.SpawnAnchor + new Vector2(DefaultTeamSeparationMeters, 0f);
    }
}
