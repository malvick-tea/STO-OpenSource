using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Garupan.Server.Match.Outcome;
using Garupan.Sim.Components;
using Garupan.Sim.Snapshot;
using Opus.Foundation;
using Opus.Net.Transport;
using Xunit;

namespace Garupan.Server.Match.Tests;

public sealed class SnapshotVisibilityFilterTests
{
    [Fact]
    public void Team_snapshot_keeps_self_team_and_nearby_enemies()
    {
        var viewer = Player(1u, Team.PlayerSchool);
        var players = new[]
        {
            viewer,
            Player(2u, Team.PlayerSchool),
            Player(3u, Team.OpponentSchool),
            Player(4u, Team.OpponentSchool),
        };
        var snapshot = new WorldSnapshot(
            Tick.Zero,
            new[]
            {
                Entity(1, Vector2.Zero),
                Entity(2, new Vector2(1000f, 0f)),
                Entity(3, new Vector2(10f, 0f)),
                Entity(4, new Vector2(1000f, 0f)),
            },
            new[]
            {
                Projectile(10, new Vector2(1000f, 0f), owner: 4),
                Projectile(11, new Vector2(5f, 0f), owner: 4),
                Projectile(12, new Vector2(1000f, 0f), owner: 2),
            });

        var visible = SnapshotVisibilityFilter.ForPeer(
            snapshot,
            viewer,
            players,
            MatchOutcomeRule.LastTeamStanding,
            visibilityRadiusMeters: 100f);

        visible.Entities.Select(row => row.Id).Should().BeEquivalentTo(new[] { 1, 2, 3 });
        visible.Projectiles.Select(row => row.Id).Should().BeEquivalentTo(new[] { 11, 12 });
    }

    [Fact]
    public void Missing_viewer_row_returns_no_replication_data()
    {
        var viewer = Player(1u, Team.PlayerSchool);
        var snapshot = new WorldSnapshot(
            Tick.Zero,
            new[] { Entity(2, Vector2.Zero) },
            Array.Empty<ProjectileSnapshot>())
        {
            Props = new[]
            {
                new PropSnapshot(1, PropState.Fallen, 0f, 1f),
            },
        };

        var visible = SnapshotVisibilityFilter.ForPeer(
            snapshot,
            viewer,
            new[] { viewer },
            MatchOutcomeRule.LastTankStanding,
            visibilityRadiusMeters: 100f);

        visible.Entities.Should().BeEmpty();
        visible.Projectiles.Should().BeEmpty();
        visible.Props.Should().BeEmpty();
    }

    private static ConnectedPlayer Player(uint networkId, Team team) => new(
        new ConnectionId(networkId),
        networkId,
        default,
        0,
        team);

    private static EntitySnapshot Entity(int id, Vector2 position) => new(
        id,
        position,
        0f,
        0f,
        EntityStateFlags.None);

    private static ProjectileSnapshot Projectile(
        int id,
        Vector2 position,
        int owner) => new(
        id,
        position,
        Vector2.Zero,
        AmmoType.AP,
        OwnerEntityId: owner);
}
