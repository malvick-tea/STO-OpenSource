using System.Collections.Generic;
using System.Numerics;
using FluentAssertions;
using Garupan.Server.Match.Outcome;
using Garupan.Sim.Components;
using Garupan.Sim.Snapshot;
using Opus.Foundation;
using Opus.Net.Transport;
using Xunit;

namespace Garupan.Server.Match.Tests;

/// <summary>Adversarial regression tests for the wallhack scrub landed in
/// Audit V2 (G-1). Each test pins one of the projectile-origin fields
/// the visibility filter must scrub when a projectile is included only
/// because it passed near the viewer (its owner is off-screen). A
/// future refactor that accidentally re-introduces the launch-position
/// leak fails loudly.</summary>
public sealed class SnapshotVisibilityFilterScrubTests
{
    private const float VisibilityRadius = 50f;
    private static readonly Vector2 ViewerPosition = new(100f, 100f);

    [Fact]
    public void Projectile_with_offscreen_owner_has_launch_position_scrubbed_to_current_position()
    {
        // The wallhack leak (audit finding G-1): a projectile fired by an
        // off-screen enemy passes near the viewer. Pre-fix, the snapshot
        // row carried the firing tank's world position in
        // LaunchPosition — a wallhack client read it directly off the
        // wire. Post-fix, LaunchPosition is replaced with the
        // projectile's current position so a near-miss reveals nothing
        // about the firer's location.
        var viewer = Player(1u, Team.PlayerSchool);
        var players = new[] { viewer };
        var projectilePosition = ViewerPosition + new Vector2(5f, 0f);
        var launchPosition = new Vector2(0f, 0f); // off-screen enemy position
        var snapshot = new WorldSnapshot(
            Tick.Zero,
            new[] { Entity(1, ViewerPosition) },
            new[]
            {
                Projectile(
                    id: 100,
                    position: projectilePosition,
                    velocity: new Vector2(1f, 0f),
                    owner: 2,
                    launchPosition: launchPosition,
                    distanceTravelled: 100f,
                    launchVisualHeight: 1.5f),
            });

        var visible = SnapshotVisibilityFilter.ForPeer(
            snapshot,
            viewer,
            players,
            MatchOutcomeRule.LastTankStanding,
            VisibilityRadius);

        visible.Projectiles.Should().ContainSingle();
        var projectile = visible.Projectiles[0];
        projectile.LaunchPosition.Should().Be(
            projectilePosition,
            "LaunchPosition must be scrubbed to the current position when the owner is off-screen so the firing tank's world position is not leaked");
        projectile.LaunchVisualHeightMeters.Should().Be(
            projectile.VisualHeightMeters,
            "LaunchVisualHeightMeters must mirror the current visual height so a height back-extrapolation does not reveal the launch point");
        projectile.Velocity.Should().Be(
            Vector2.Zero,
            "Velocity must be zeroed when the owner is off-screen so a back-extrapolation to the launch position is impossible");
        projectile.DistanceTravelledMeters.Should().Be(
            0f,
            "DistanceTravelledMeters must be zeroed when the owner is off-screen so the launch position cannot be back-computed");
    }

    [Fact]
    public void Projectile_with_visible_owner_keeps_launch_position_intact()
    {
        // Sanity check: the scrub only fires when the owner is off-screen.
        // When the owner is visible to the viewer, the launch position is
        // already known (the tank itself is in the snapshot), so the
        // scrub would just destroy useful telemetry.
        var viewer = Player(1u, Team.PlayerSchool);
        var players = new[] { viewer, Player(2u, Team.OpponentSchool) };
        var enemyPosition = ViewerPosition + new Vector2(10f, 0f);
        var launchPosition = enemyPosition;
        var projectilePosition = enemyPosition + new Vector2(5f, 0f);
        var snapshot = new WorldSnapshot(
            Tick.Zero,
            new[]
            {
                Entity(1, ViewerPosition),
                Entity(2, enemyPosition),
            },
            new[]
            {
                Projectile(
                    id: 100,
                    position: projectilePosition,
                    velocity: new Vector2(1f, 0f),
                    owner: 2,
                    launchPosition: launchPosition,
                    distanceTravelled: 5f,
                    launchVisualHeight: 1.5f),
            });

        var visible = SnapshotVisibilityFilter.ForPeer(
            snapshot,
            viewer,
            players,
            MatchOutcomeRule.LastTankStanding,
            VisibilityRadius);

        visible.Projectiles.Should().ContainSingle();
        var projectile = visible.Projectiles[0];
        projectile.LaunchPosition.Should().Be(
            launchPosition,
            "LaunchPosition must be preserved when the owner is visible — the viewer already knows the firer's position");
        projectile.Velocity.Should().NotBe(Vector2.Zero);
        projectile.DistanceTravelledMeters.Should().Be(5f);
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
        Vector2 velocity,
        int owner,
        Vector2 launchPosition,
        float distanceTravelled,
        float launchVisualHeight) => new(
        id,
        position,
        velocity,
        AmmoType.AP,
        VisualHeightMeters: 0f,
        VerticalVelocityMps: 0f,
        DistanceTravelledMeters: distanceTravelled,
        LaunchPosition: launchPosition,
        LaunchVisualHeightMeters: launchVisualHeight,
        OwnerEntityId: owner);
}
