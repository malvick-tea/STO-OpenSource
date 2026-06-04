using System.Numerics;
using FluentAssertions;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Systems;
using Opus.Foundation;
using Xunit;

namespace Garupan.Sim.Tests.Systems;

/// <summary>
/// Behavioural coverage for <see cref="RespawnSystem"/>: lives are decremented on
/// knock-out, a timer queues the comeback, the timer counts down to zero, the tank
/// reappears at its spawn anchor, and a tank with no remaining lives stays a wreck.
/// </summary>
public sealed class RespawnSystemTests
{
    private const ushort TestDelayTicks = 5;

    [Fact]
    public void A_tank_without_RespawnLives_is_invisible_to_the_system()
    {
        using var world = World.Create();
        var tank = world.Spawn(new Transform(Vector2.Zero, 0f));
        world.Add(tank, default(KnockedOut));

        new RespawnSystem(TestDelayTicks).Tick(MakeCtx(world));

        world.Has<RespawnTimer>(tank).Should().BeFalse();
        world.Has<KnockedOut>(tank).Should().BeTrue("a single-life tank stays a wreck");
    }

    [Fact]
    public void First_tick_after_knockout_decrements_lives_and_queues_a_timer()
    {
        using var world = World.Create();
        var tank = SpawnAt(world, position: new Vector2(5f, 6f), yaw: 0.5f, livesRemaining: 3);
        world.Add(tank, default(KnockedOut));

        new RespawnSystem(TestDelayTicks).Tick(MakeCtx(world));

        world.Has<RespawnTimer>(tank).Should().BeTrue();
        world.Get<RespawnTimer>(tank).TicksRemaining.Should().Be(TestDelayTicks);
        world.Get<RespawnLives>(tank).Remaining.Should().Be(2, "one respawn was consumed on queue");
        world.Has<KnockedOut>(tank).Should().BeTrue("the tag stays until the timer expires");
    }

    [Fact]
    public void Subsequent_ticks_count_down_the_timer()
    {
        using var world = World.Create();
        var tank = SpawnAt(world, Vector2.Zero, 0f, livesRemaining: 2);
        world.Add(tank, default(KnockedOut));
        var system = new RespawnSystem(TestDelayTicks);

        system.Tick(MakeCtx(world));
        system.Tick(MakeCtx(world));
        system.Tick(MakeCtx(world));

        world.Get<RespawnTimer>(tank).TicksRemaining.Should().Be((ushort)(TestDelayTicks - 2));
    }

    [Fact]
    public void Timer_expiry_clears_KnockedOut_and_restores_spawn_transform()
    {
        using var world = World.Create();
        var spawn = new Vector2(7f, 11f);
        const float SpawnYaw = 1.3f;
        var tank = SpawnAt(world, spawn, SpawnYaw, livesRemaining: 1);
        // Move the tank somewhere else mid-match so the spawn anchor is meaningful.
        world.Set(tank, new Transform(new Vector2(100f, 100f), 0f));
        world.Add(tank, default(KnockedOut));
        world.AddOrSet(tank, new DriveInput { Throttle = 1f, Steering = 0.5f });
        world.AddOrSet(tank, new TurretTarget { YawRadians = 3.14f });
        var system = new RespawnSystem(respawnDelayTicks: 1);

        // First tick queues the timer with TicksRemaining = 1.
        system.Tick(MakeCtx(world));
        // Second tick decrements to 0, and the respawn pass restores the tank.
        system.Tick(MakeCtx(world));

        world.Has<KnockedOut>(tank).Should().BeFalse();
        world.Has<RespawnTimer>(tank).Should().BeFalse();
        world.Get<RespawnLives>(tank).Remaining.Should().Be(0, "the queue consumed the budget");
        world.Get<Transform>(tank).Position.Should().Be(spawn);
        world.Get<Transform>(tank).YawRadians.Should().BeApproximately(SpawnYaw, 1e-5f);
        world.Get<DriveInput>(tank).Throttle.Should().Be(0f);
        world.Get<DriveInput>(tank).Steering.Should().Be(0f);
        world.Get<TurretTarget>(tank).YawRadians.Should().BeApproximately(SpawnYaw, 1e-5f);
    }

    [Fact]
    public void A_knockout_with_no_lives_left_stays_a_wreck()
    {
        using var world = World.Create();
        var tank = SpawnAt(world, Vector2.Zero, 0f, livesRemaining: 0);
        world.Add(tank, default(KnockedOut));

        new RespawnSystem(TestDelayTicks).Tick(MakeCtx(world));

        world.Has<RespawnTimer>(tank).Should().BeFalse();
        world.Has<KnockedOut>(tank).Should().BeTrue();
    }

    [Fact]
    public void Multiple_knockouts_consume_one_life_per_cycle()
    {
        using var world = World.Create();
        var tank = SpawnAt(world, Vector2.Zero, 0f, livesRemaining: 3);
        var system = new RespawnSystem(respawnDelayTicks: 1);

        for (var cycle = 0; cycle < 3; cycle++)
        {
            world.Add(tank, default(KnockedOut));
            system.Tick(MakeCtx(world));
            system.Tick(MakeCtx(world));
            world.Has<KnockedOut>(tank).Should().BeFalse("respawn cycle should clear the tag");
        }

        world.Get<RespawnLives>(tank).Remaining.Should().Be(0);

        // Final knock-out — no respawn budget left.
        world.Add(tank, default(KnockedOut));
        system.Tick(MakeCtx(world));

        world.Has<RespawnTimer>(tank).Should().BeFalse();
        world.Has<KnockedOut>(tank).Should().BeTrue();
    }

    [Fact]
    public void Default_respawn_delay_is_two_seconds_at_thirty_hz()
    {
        // 60 ticks at the canonical 30Hz tick rate is exactly 2.0s — long enough for the
        // player to read the knock-out beat, short enough not to break match pacing.
        RespawnSystem.DefaultRespawnDelayTicks.Should().Be(60);
    }

    [Fact]
    public void Respawn_grants_a_spawn_invulnerability_tag_with_the_configured_window()
    {
        using var world = World.Create();
        var tank = SpawnAt(world, Vector2.Zero, 0f, livesRemaining: 1);
        world.Add(tank, default(KnockedOut));
        const ushort InvulnTicks = 42;
        var system = new RespawnSystem(respawnDelayTicks: 1, spawnInvulnerabilityTicks: InvulnTicks);

        system.Tick(MakeCtx(world));
        system.Tick(MakeCtx(world));

        world.Has<RespawnInvulnerable>(tank).Should().BeTrue(
            "the returning crew needs a brief shield against spawn-campers");
        world.Get<RespawnInvulnerable>(tank).TicksRemaining.Should().Be(InvulnTicks);
    }

    [Fact]
    public void A_zero_invulnerability_window_opts_the_respawn_out_of_the_tag()
    {
        // Single-player / determinism scenarios pass 0 to keep legacy no-shield behaviour.
        using var world = World.Create();
        var tank = SpawnAt(world, Vector2.Zero, 0f, livesRemaining: 1);
        world.Add(tank, default(KnockedOut));
        var system = new RespawnSystem(respawnDelayTicks: 1, spawnInvulnerabilityTicks: 0);

        system.Tick(MakeCtx(world));
        system.Tick(MakeCtx(world));

        world.Has<RespawnInvulnerable>(tank).Should().BeFalse();
    }

    private static EntityHandle SpawnAt(World world, Vector2 position, float yaw, byte livesRemaining)
    {
        var entity = world.Spawn(new Transform(position, yaw));
        world.Add(entity, new RespawnLives
        {
            Remaining = livesRemaining,
            SpawnPosition = position,
            SpawnYawRadians = yaw,
        });
        return entity;
    }

    private static TickContext MakeCtx(World world) =>
        new(world, GameTime.AtRate(60).Advance(), SimSeed.Zero, new CommandBuffer());
}
