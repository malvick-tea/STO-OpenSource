using System;
using System.Collections.Generic;
using System.Numerics;
using Garupan.Server.Match.Outcome;
using Garupan.Sim.Snapshot;

namespace Garupan.Server.Match;

internal static class SnapshotVisibilityFilter
{
    public static WorldSnapshot ForPeer(
        WorldSnapshot snapshot,
        ConnectedPlayer viewer,
        IReadOnlyCollection<ConnectedPlayer> players,
        MatchOutcomeRule outcomeRule,
        float visibilityRadiusMeters)
    {
        var viewerPosition = FindViewerPosition(snapshot.Entities, viewer.NetworkId);
        if (viewerPosition is null)
        {
            return new WorldSnapshot(
                snapshot.Tick,
                Array.Empty<EntitySnapshot>(),
                Array.Empty<ProjectileSnapshot>());
        }

        var teams = BuildTeamMap(players);
        var radiusSquared = visibilityRadiusMeters * visibilityRadiusMeters;
        var visibleEntities = new List<EntitySnapshot>(snapshot.Entities.Count);
        var visibleIds = new HashSet<int>();
        foreach (var entity in snapshot.Entities)
        {
            if ((uint)entity.Id == viewer.NetworkId)
            {
                visibleEntities.Add(entity);
                visibleIds.Add(entity.Id);
                break;
            }
        }

        foreach (var entity in snapshot.Entities)
        {
            var isSelf = (uint)entity.Id == viewer.NetworkId;
            var isTeamMate = outcomeRule == MatchOutcomeRule.LastTeamStanding
                && teams.TryGetValue((uint)entity.Id, out var team)
                && team == viewer.Team;
            var isNear = Vector2.DistanceSquared(viewerPosition.Value, entity.Position) <= radiusSquared;
            if (isSelf || (!isTeamMate && !isNear))
            {
                continue;
            }

            if (visibleEntities.Count >= SnapshotWire.MaxEntities)
            {
                break;
            }

            visibleEntities.Add(entity);
            visibleIds.Add(entity.Id);
        }

        var visibleProjectiles = new List<ProjectileSnapshot>(snapshot.Projectiles.Count);
        foreach (var projectile in snapshot.Projectiles)
        {
            var ownerVisible = visibleIds.Contains(projectile.OwnerEntityId);
            var nearViewer = Vector2.DistanceSquared(viewerPosition.Value, projectile.Position) <= radiusSquared;
            if (!ownerVisible && !nearViewer)
            {
                continue;
            }

            // When the projectile is included only because it passed near the
            // viewer (the firing tank is off-screen), strip the launch-origin
            // fields so a wallhack client cannot read the enemy's world
            // position from a near-miss projectile. LaunchPosition is replaced
            // with the projectile's current position, Velocity is zeroed, and
            // DistanceTravelledMeters + LaunchVisualHeightMeters are zeroed so
            // back-extrapolation is impossible. The owner id is preserved so
            // client-side hit resolution still attributes damage correctly.
            var row = projectile;
            if (!ownerVisible)
            {
                row = row with
                {
                    LaunchPosition = row.Position,
                    LaunchVisualHeightMeters = row.VisualHeightMeters,
                    Velocity = Vector2.Zero,
                    DistanceTravelledMeters = 0f,
                };
            }

            visibleProjectiles.Add(row);
            if (visibleProjectiles.Count >= SnapshotWire.MaxProjectiles)
            {
                break;
            }
        }

        return new WorldSnapshot(snapshot.Tick, visibleEntities, visibleProjectiles)
        {
            Props = LimitProps(snapshot.Props),
        };
    }

    private static Vector2? FindViewerPosition(
        IReadOnlyList<EntitySnapshot> entities,
        uint networkId)
    {
        foreach (var entity in entities)
        {
            if ((uint)entity.Id == networkId)
            {
                return entity.Position;
            }
        }

        return null;
    }

    private static Dictionary<uint, Garupan.Sim.Components.Team> BuildTeamMap(
        IReadOnlyCollection<ConnectedPlayer> players)
    {
        var teams = new Dictionary<uint, Garupan.Sim.Components.Team>(players.Count);
        foreach (var player in players)
        {
            teams[player.NetworkId] = player.Team;
        }

        return teams;
    }

    private static IReadOnlyList<PropSnapshot> LimitProps(IReadOnlyList<PropSnapshot> props)
    {
        if (props.Count <= SnapshotWire.MaxProps)
        {
            return props;
        }

        var limited = new PropSnapshot[SnapshotWire.MaxProps];
        for (var index = 0; index < limited.Length; index++)
        {
            limited[index] = props[index];
        }

        return limited;
    }
}
