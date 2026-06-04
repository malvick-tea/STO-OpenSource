using System.Linq;
using System.Numerics;
using Arch.Core;
using FluentAssertions;
using Garupan.Content;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Spawn;
using Garupan.Sim.Systems;
using Opus.Foundation;
using Xunit;

namespace Garupan.Sim.Tests.Systems;

public sealed class GunFireAndLifecycleTests
{
    [Fact]
    public void Reload_tick_decrements_timer()
    {
        using var world = World.Create();
        var tank = world.Spawn(new Gun { ReloadSeconds = 1f, ReloadSecondsMax = 7.5f });

        new ReloadTickSystem().Tick(MakeCtx(world));

        var dt = 1f / 60f;
        world.Get<Gun>(tank).ReloadSeconds.Should().BeApproximately(1f - dt, 0.001f);
    }

    [Fact]
    public void Reload_tick_clamps_at_zero()
    {
        using var world = World.Create();
        var tank = world.Spawn(new Gun { ReloadSeconds = 0.001f, ReloadSecondsMax = 7.5f });

        new ReloadTickSystem().Tick(MakeCtx(world));

        world.Get<Gun>(tank).ReloadSeconds.Should().Be(0f);
    }

    [Fact]
    public void Gun_fire_spawns_projectile_with_world_aim()
    {
        using var world = World.Create();
        var shooter = world.Spawn(
            new Transform(new Vector2(10f, 20f), MathF.PI / 2f),
            new Turret { YawRadians = 0f, BarrelPitchRadians = 0.2f },
            new Gun
            {
                ReloadSeconds = 0f,
                ReloadSecondsMax = 7.5f,
                Chambered = Round(penetrationMm: 99f),
            });
        world.Add(shooter, Mount());
        world.Add(shooter, default(FireIntent));

        new GunFireSystem().Tick(MakeCtx(world));

        // FireIntent removed.
        world.Has<FireIntent>(shooter).Should().BeFalse();

        // Reload reset.
        world.Get<Gun>(shooter).ReloadSeconds.Should().Be(7.5f);

        // Projectile spawned at the pitched muzzle with velocity along the world-frame
        // turret yaw. Hull yaw is deliberately different so adding it twice is caught.
        var projectiles = QueryProjectiles(world).ToList();
        projectiles.Should().HaveCount(1);
        var projTf = world.Raw.Get<Transform>(projectiles[0]);
        var projData = world.Raw.Get<Projectile>(projectiles[0]);
        projTf.Position.Should().Be(Mount().MuzzlePosition(new Vector2(10f, 20f), 0f, 0.2f));
        projData.Velocity.X.Should().BeApproximately(750f * MathF.Cos(0.2f), 0.001f);
        projData.Velocity.Y.Should().BeApproximately(0f, 0.001f);
        projData.VerticalVelocityMps.Should().BeApproximately(750f * MathF.Sin(0.2f), 0.001f);
        projData.VisualHeightMeters.Should().BeApproximately(Mount().MuzzleHeightMeters(0.2f), 0.001f);
        projData.LaunchPosition.Should().Be(projTf.Position);
        projData.LaunchVisualHeightMeters.Should().Be(projData.VisualHeightMeters);
        projData.Dynamics.Should().NotBeNull();
        projData.Penetration.Normal500Mm.Should().Be(99f);
    }

    [Fact]
    public void Firing_a_pz_iv_recoils_the_gun_while_compacted_ground_holds_the_hull()
    {
        using var world = World.Create();
        var hull = ChassisBuilder.Build(TankRoster.VehicleMediumA);
        var tank = world.Spawn(
            new Transform(Vector2.Zero, 0f),
            new Turret { YawRadians = 0f, BarrelPitchRadians = 0f },
            hull);
        var gun = TurretGunBuilder.BuildGun(TankRoster.VehicleMediumA.Gun);
        gun.ReloadSeconds = 0f;
        world.Add(tank, gun);
        world.Add(tank, Mount());
        world.Add(tank, default(FireIntent));

        new GunFireSystem().Tick(MakeCtx(world));

        // Gun aimed due east (+X); recoil is the shot momentum over the hull mass, pushing −X.
        var velocity = world.Get<Hull>(tank).DynamicsState.VelocityMps;
        velocity.X.Should().Be(0f, "track-ground static grip holds the medium tank discharge");
        velocity.Y.Should().BeApproximately(0f, 0.0001f);
        world.Get<GunRecoilState>(tank).TravelMeters.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Firing_a_heavy_howitzer_slides_the_hull_backward_when_grip_is_exceeded()
    {
        using var world = World.Create();
        var hull = ChassisBuilder.Build(TankRoster.VehicleHeavyD);
        var tank = world.Spawn(
            new Transform(Vector2.Zero, 0f),
            new Turret { YawRadians = 0f, BarrelPitchRadians = 0f },
            hull);
        var gun = TurretGunBuilder.BuildGun(TankRoster.VehicleHeavyD.Gun);
        gun.ReloadSeconds = 0f;
        world.Add(tank, gun);
        world.Add(tank, Mount());
        world.Add(tank, default(FireIntent));

        new GunFireSystem().Tick(MakeCtx(world));

        world.Get<Hull>(tank).DynamicsState.VelocityMps.X.Should().BeLessThan(0f);
    }

    [Fact]
    public void Gun_recoil_tick_returns_the_mount_to_battery_without_allocating_cleanup_work()
    {
        using var world = World.Create();
        var tank = world.Spawn(new GunRecoilState
        {
            TravelMeters = 0.01f,
            ReturnSpeedMetersPerSecond = 1f,
        });

        new GunRecoilTickSystem().Tick(MakeCtx(world));

        world.Get<GunRecoilState>(tank).TravelMeters.Should().Be(0f);
    }

    [Fact]
    public void A_hullless_shooter_fires_without_recoil()
    {
        using var world = World.Create();
        var shooter = world.Spawn(
            new Transform(Vector2.Zero, 0f),
            new Turret { YawRadians = 0f },
            new Gun { Chambered = Round() });
        world.Add(shooter, Mount());
        world.Add(shooter, default(FireIntent));

        var act = () => new GunFireSystem().Tick(MakeCtx(world));

        act.Should().NotThrow();
        QueryProjectiles(world).Should().ContainSingle();
    }

    [Fact]
    public void Pipeline_keeps_a_new_projectile_at_the_muzzle_until_the_next_tick()
    {
        using var world = World.Create();
        var shooter = world.Spawn(
            new Transform(Vector2.Zero, 0f),
            new Turret { YawRadians = 0f },
            new Gun
            {
                Chambered = Round(muzzleVelocityMps: 60f),
            });
        world.Add(shooter, Mount());
        world.Add(shooter, default(FireIntent));
        var pipeline = new SystemPipeline(new ISystem[]
        {
            new GunFireSystem(),
            new ProjectileIntegrateSystem(),
        });
        var time = GameTime.AtRate(60).Advance();

        pipeline.Tick(world, time, SimSeed.Zero);

        var projectile = QueryProjectiles(world).Should().ContainSingle().Which;
        var muzzle = Mount().MuzzlePosition(Vector2.Zero, 0f, 0f);
        world.Raw.Get<Transform>(projectile).Position.Should().Be(muzzle);

        pipeline.Tick(world, time.Advance(), SimSeed.Zero);

        world.Raw.Get<Transform>(projectile).Position.Should().Be(muzzle + Vector2.UnitX);
    }

    [Fact]
    public void Pz_iv_mount_uses_the_turret_face_trunnions_while_preserving_the_neutral_muzzle()
    {
        var mount = Mount();

        mount.TrunnionForwardMeters.Should().BeApproximately(1.037121f, 0.000001f);
        mount.ForwardMeters(0f).Should().BeApproximately(4.097617f, 0.000001f);
        mount.MuzzleHeightMeters(0f).Should().BeApproximately(1.942544f, 0.000001f);
        mount.MuzzleHeightMeters(0.2f).Should().BeApproximately(
            1.942544f + (MathF.Sin(0.2f) * 3.060496f),
            0.000001f);
    }

    [Fact]
    public void Gun_fire_skips_if_on_cooldown()
    {
        using var world = World.Create();
        var shooter = world.Spawn(
            new Transform(Vector2.Zero, 0f),
            new Turret { YawRadians = 0f },
            new Gun
            {
                ReloadSeconds = 3f, // still loading
                ReloadSecondsMax = 7.5f,
                Chambered = new ChamberedRound { MuzzleVelocityMps = 750f, Penetration = PenetrationProfile.Flat(99f) },
            });
        world.Add(shooter, Mount());
        world.Add(shooter, default(FireIntent));

        new GunFireSystem().Tick(MakeCtx(world));

        // Intent still cleared, but no projectile.
        world.Has<FireIntent>(shooter).Should().BeFalse();
        QueryProjectiles(world).Should().BeEmpty();
    }

    [Fact]
    public void Lifetime_decay_tags_dead_when_expired()
    {
        using var world = World.Create();
        var bullet = world.Spawn(new Lifetime { SecondsRemaining = 0.001f });

        new LifetimeDecaySystem().Tick(MakeCtx(world));

        world.Has<Dead>(bullet).Should().BeTrue();
    }

    [Fact]
    public void Cleanup_dead_destroys_tagged_entities()
    {
        using var world = World.Create();
        var alive = world.Spawn(new Transform(Vector2.Zero, 0f));
        var doomed = world.Spawn(new Transform(Vector2.One, 0f));
        world.Add(doomed, default(Dead));

        new CleanupDeadSystem().Tick(MakeCtx(world));

        world.IsAlive(alive).Should().BeTrue();
        world.IsAlive(doomed).Should().BeFalse();
    }

    private static System.Collections.Generic.List<Entity> QueryProjectiles(World world)
    {
        var list = new System.Collections.Generic.List<Entity>();
        var query = new QueryDescription().WithAll<Projectile>();
        world.Raw.Query(in query, (Entity e) => list.Add(e));
        return list;
    }

    private static TickContext MakeCtx(World world) =>
        new(world, GameTime.AtRate(60).Advance(), SimSeed.Zero, new CommandBuffer());

    private static ChamberedRound Round(float muzzleVelocityMps = 750f, float penetrationMm = 0f) => new()
    {
        Type = Garupan.Sim.Components.AmmoType.AP,
        MuzzleVelocityMps = muzzleVelocityMps,
        MassKg = 6.8f,
        DiameterMeters = 0.075f,
        DragCoefficient = 0f,
        Penetration = PenetrationProfile.Flat(penetrationMm),
    };

    private static GunMount Mount() => TurretGunBuilder.BuildGunMount(GunMountCatalog.MountMediumA);
}
