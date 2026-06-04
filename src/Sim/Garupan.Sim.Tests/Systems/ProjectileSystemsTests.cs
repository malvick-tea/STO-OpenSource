using System.Numerics;
using FluentAssertions;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Systems;
using Opus.Engine.Physics.Ballistics;
using Opus.Foundation;
using Xunit;

namespace Garupan.Sim.Tests.Systems;

public sealed class ProjectileSystemsTests
{
    [Fact]
    public void Integrate_advances_position_by_velocity_dt()
    {
        using var world = World.Create();
        var dt = 1.0 / 60.0;

        var bullet = world.Spawn(
            new Transform(Vector2.Zero, 0f),
            new Projectile
            {
                Velocity = new Vector2(750f, 0f),
                MassKg = 6.8f,
                Type = AmmoType.AP,
                Penetration = PenetrationProfile.Flat(99f),
            });

        var pipeline = new SystemPipeline(new[] { new ProjectileIntegrateSystem() });
        pipeline.Tick(world, GameTime.AtRate(60).Advance(), SimSeed.Zero);

        var pos = world.Get<Transform>(bullet).Position;
        pos.X.Should().BeApproximately(750f * (float)dt, 0.001f);
    }

    [Fact]
    public void Physical_integrate_applies_drag_gravity_and_distance()
    {
        using var world = World.Create();
        var bullet = world.Spawn(
            new Transform(Vector2.Zero, 0f),
            new Projectile
            {
                Velocity = new Vector2(750f, 0f),
                VisualHeightMeters = 2f,
                Dynamics = BallisticBodyProperties.FromDiameter(
                    6.8f,
                    0.075f,
                    new ConstantDragCoefficientCurve(0.3f)),
            });

        new ProjectileIntegrateSystem().Tick(MakeCtx(world));

        var projectile = world.Get<Projectile>(bullet);
        projectile.Velocity.X.Should().BeLessThan(750f);
        projectile.VisualHeightMeters.Should().BeLessThan(2f);
        projectile.DistanceTravelledMeters.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Ground_impact_is_consumed_after_swept_resolution()
    {
        using var world = World.Create();
        var bullet = world.Spawn(
            new Transform(Vector2.Zero, 0f),
            new Projectile
            {
                Velocity = new Vector2(10f, 0f),
                VisualHeightMeters = 0.001f,
                VerticalVelocityMps = -10f,
                Dynamics = BallisticBodyProperties.FromDiameter(
                    1f,
                    0.01f,
                    new ConstantDragCoefficientCurve(0f)),
            });

        new ProjectileIntegrateSystem().Tick(MakeCtx(world));
        world.Get<Projectile>(bullet).HitGround.Should().BeTrue();

        new ProjectileHitResolveSystem().Tick(MakeCtx(world));
        world.Has<Dead>(bullet).Should().BeTrue();
    }

    [Fact]
    public void Swept_segment_hits_target_crossed_between_ticks()
    {
        using var world = World.Create();
        var target = SpawnTank(world, hullFrontMm: 20f, hullSideMm: 20f, hullRearMm: 20f);
        world.Spawn(
            new Transform(new Vector2(10f, 0f), 0f),
            new Projectile
            {
                PreviousPosition = new Vector2(-10f, 0f),
                PreviousVisualHeightMeters = 1f,
                VisualHeightMeters = 1f,
                HasIntegratedSegment = true,
                Penetration = PenetrationProfile.Flat(50f),
                Type = AmmoType.AP,
            });

        new ProjectileHitResolveSystem().Tick(MakeCtx(world));

        world.Has<KnockedOut>(target).Should().BeTrue();
    }

    [Fact]
    public void Swept_segment_above_silhouette_misses_target()
    {
        using var world = World.Create();
        var target = SpawnTank(world, hullFrontMm: 20f);
        world.Spawn(
            new Transform(new Vector2(10f, 0f), 0f),
            new Projectile
            {
                PreviousPosition = new Vector2(-10f, 0f),
                PreviousVisualHeightMeters = 5f,
                VisualHeightMeters = 5f,
                HasIntegratedSegment = true,
                Penetration = PenetrationProfile.Flat(50f),
            });

        new ProjectileHitResolveSystem().Tick(MakeCtx(world));

        world.Has<KnockedOut>(target).Should().BeFalse();
    }

    [Fact]
    public void Round_defeats_a_plate_at_close_range_but_not_at_long_range()
    {
        // Same round (150 mm near, 60 mm at 1000 m) against the same 80 mm plate: the impact
        // range — distance from launch to impact — drives the penetration sampled from the table.
        var profile = new PenetrationProfile { Normal100Mm = 150f, Normal500Mm = 100f, Normal1000Mm = 60f };

        ResolveSingleShot(launch: new Vector2(0f, 0f), impact: new Vector2(80f, 0f), profile, plateMm: 80f)
            .Should().BeTrue("at point-blank the round's 150 mm penetration defeats the 80 mm plate");

        ResolveSingleShot(launch: new Vector2(0f, 0f), impact: new Vector2(1000f, 0f), profile, plateMm: 80f)
            .Should().BeFalse("at ~1000 m the round has fallen to 60 mm and the 80 mm plate holds");
    }

    [Fact]
    public void Sloped_plate_defeats_a_round_that_would_pass_it_vertical()
    {
        // 90 mm penetration versus an 80 mm plate: through when vertical, held at the medium tank E's 60°.
        var profile = PenetrationProfile.Flat(90f);

        ResolveSingleShot(Vector2.Zero, new Vector2(50f, 0f), profile, plateMm: 80f, plateSlopeDeg: 0f)
            .Should().BeTrue("a vertical 80 mm plate is line-of-sight 80 mm");

        ResolveSingleShot(Vector2.Zero, new Vector2(50f, 0f), profile, plateMm: 80f, plateSlopeDeg: 60f)
            .Should().BeFalse("at 60° the 80 mm plate is ~160 mm line-of-sight");
    }

    [Fact]
    public void Front_hit_with_enough_pen_knocks_target_out()
    {
        using var world = World.Create();
        var target = SpawnTank(world, hullFrontMm: 80f, position: new Vector2(0f, 0f), yaw: 0f);

        var bullet = world.Spawn(
            new Transform(new Vector2(0f, 0f), 0f),
            new Projectile { Penetration = PenetrationProfile.Flat(132f), Type = AmmoType.AP });

        new ProjectileHitResolveSystem().Tick(MakeCtx(world));

        world.Has<KnockedOut>(target).Should().BeTrue();
        world.Has<Dead>(bullet).Should().BeTrue();
    }

    [Fact]
    public void Front_hit_with_insufficient_pen_bounces()
    {
        using var world = World.Create();
        var target = SpawnTank(world, hullFrontMm: 200f, hullSideMm: 200f, hullRearMm: 200f, position: new Vector2(0f, 0f), yaw: 0f);

        var bullet = world.Spawn(
            new Transform(new Vector2(1f, 0f), 0f),
            new Projectile { Penetration = PenetrationProfile.Flat(99f), Type = AmmoType.AP });

        new ProjectileHitResolveSystem().Tick(MakeCtx(world));

        world.Has<KnockedOut>(target).Should().BeFalse();
        world.Has<Dead>(bullet).Should().BeTrue(); // projectile spent regardless
    }

    [Fact]
    public void Rear_hit_uses_rear_armour_value()
    {
        using var world = World.Create();
        var target = SpawnTank(world, hullFrontMm: 200f, hullRearMm: 20f, position: new Vector2(0f, 0f), yaw: 0f);

        var bullet = world.Spawn(
            new Transform(new Vector2(-1f, 0f), 0f),
            new Projectile { Penetration = PenetrationProfile.Flat(50f), Type = AmmoType.AP });

        new ProjectileHitResolveSystem().Tick(MakeCtx(world));

        world.Has<KnockedOut>(target).Should().BeTrue(); // 50mm pen vs 20mm rear → through
    }

    [Fact]
    public void Side_hit_uses_side_armour_value()
    {
        using var world = World.Create();
        var target = SpawnTank(world, hullFrontMm: 200f, hullSideMm: 30f, position: new Vector2(0f, 0f), yaw: 0f);

        var bullet = world.Spawn(
            new Transform(new Vector2(0f, 1f), 0f),
            new Projectile { Penetration = PenetrationProfile.Flat(50f), Type = AmmoType.AP });

        new ProjectileHitResolveSystem().Tick(MakeCtx(world));

        world.Has<KnockedOut>(target).Should().BeTrue();
    }

    [Fact]
    public void Miss_does_nothing()
    {
        using var world = World.Create();
        var target = SpawnTank(world, hullFrontMm: 80f, position: new Vector2(0f, 0f), yaw: 0f);

        var bullet = world.Spawn(
            new Transform(new Vector2(50f, 0f), 0f),
            new Projectile { Penetration = PenetrationProfile.Flat(200f), Type = AmmoType.AP });

        new ProjectileHitResolveSystem().Tick(MakeCtx(world));

        world.Has<KnockedOut>(target).Should().BeFalse();
        world.Has<Dead>(bullet).Should().BeFalse();
    }

    [Fact]
    public void Spawn_invulnerable_target_is_skipped_by_hit_resolution()
    {
        using var world = World.Create();
        var target = SpawnTank(world, hullFrontMm: 80f, position: new Vector2(0f, 0f), yaw: 0f);
        world.Add(target, new RespawnInvulnerable { TicksRemaining = 30 });

        var bullet = world.Spawn(
            new Transform(new Vector2(0f, 0f), 0f),
            new Projectile { Penetration = PenetrationProfile.Flat(132f), Type = AmmoType.AP });

        new ProjectileHitResolveSystem().Tick(MakeCtx(world));

        world.Has<KnockedOut>(target).Should().BeFalse("the respawn shield is in force");
        world.Has<Dead>(bullet).Should().BeFalse(
            "a shielded tank is intangible — the projectile is not consumed by the would-be impact");
    }

    /// <summary>Fires one stationary round at a single-plate tank and returns whether it defeated
    /// the plate. The tank's hull armour is uniform so the impact sector does not matter; the
    /// round carries no velocity, so the obliquity reduces to the plate slope.</summary>
    private static bool ResolveSingleShot(
        Vector2 launch, Vector2 impact, PenetrationProfile profile, float plateMm, float plateSlopeDeg = 0f)
    {
        using var world = World.Create();
        var target = SpawnTank(
            world, hullFrontMm: plateMm, hullSideMm: plateMm, hullRearMm: plateMm,
            hullSlopeDeg: plateSlopeDeg, position: impact);
        world.Spawn(
            new Transform(impact, 0f),
            new Projectile { LaunchPosition = launch, Penetration = profile, Type = AmmoType.AP });

        new ProjectileHitResolveSystem().Tick(MakeCtx(world));
        return world.Has<KnockedOut>(target);
    }

    private static EntityHandle SpawnTank(
        World world,
        float hullFrontMm = 80f,
        float hullSideMm = 30f,
        float hullRearMm = 20f,
        float hullSlopeDeg = 0f,
        Vector2 position = default,
        float yaw = 0f)
    {
        return world.Spawn(
            new Transform(position, yaw),
            new Hull
            {
                Type = TankId.None,
                Armor = new ArmorMap
                {
                    HullFrontMm = hullFrontMm,
                    HullFrontSlopeDeg = hullSlopeDeg,
                    HullSideMm = hullSideMm,
                    HullSideSlopeDeg = hullSlopeDeg,
                    HullRearMm = hullRearMm,
                    HullRearSlopeDeg = hullSlopeDeg,
                },
            },
            new HitRadius { Meters = 1.5f });
    }

    private static TickContext MakeCtx(World world)
    {
        var time = GameTime.AtRate(60);
        return new TickContext(world, time, SimSeed.Zero, new CommandBuffer());
    }
}
