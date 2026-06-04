using System.Collections.Generic;
using Arch.Core;
using Garupan.Sim.Components;

namespace Garupan.Sim.Systems;

/// <summary>
/// Manages the per-tank respawn cycle for multi-life match modes.
///
/// On a tank that has <see cref="KnockedOut"/> + <see cref="RespawnLives"/> but no
/// <see cref="RespawnTimer"/> yet: decrement the life counter and attach a timer for
/// the configured delay. On a tank that already has <see cref="RespawnTimer"/>: count
/// it down; once it hits zero, clear <see cref="KnockedOut"/>, restore the tank's
/// <see cref="Transform"/> to the stored spawn anchor + zero its <see cref="DriveInput"/>,
/// then remove the timer. A tank without <see cref="RespawnLives"/> (single-player
/// canon missions, determinism scenarios) is invisible to this system — it stays a
/// permanent wreck on the field, the legacy Phase-0 behaviour.
///
/// Order: 650 — runs immediately after <see cref="ProjectileHitResolveSystem"/> so a
/// hit landed at tick t is observed + respawn-queued in the same tick. The transition
/// "alive → knocked + timer queued" therefore lands inside one tick, which is what
/// <see cref="Garupan.Server.Match.MatchHost"/>'s outcome evaluator reads after the
/// pipeline finishes.
/// Ported from <c>svo/engine/src/systems/respawn.cpp</c> (conceptual — no direct mapping).
/// </summary>
public sealed class RespawnSystem : IFixedSystem
{
    public string Name => "Respawn";

    public int Order => 650;

    /// <summary>Default respawn delay in ticks. At 30 Hz this is two seconds — long
    /// enough for the player to read the "you were knocked out" beat, short enough
    /// not to break the match's pacing.</summary>
    public const ushort DefaultRespawnDelayTicks = 60;

    private readonly ushort _respawnDelayTicks;
    private readonly ushort _spawnInvulnerabilityTicks;
    private readonly List<Entity> _toQueue = new();
    private readonly List<Entity> _toRespawn = new();

    public RespawnSystem(
        ushort respawnDelayTicks = DefaultRespawnDelayTicks,
        ushort spawnInvulnerabilityTicks = SpawnInvulnerabilitySystem.DefaultInvulnerabilityTicks)
    {
        _respawnDelayTicks = respawnDelayTicks;
        _spawnInvulnerabilityTicks = spawnInvulnerabilityTicks;
    }

    public void Tick(in TickContext ctx)
    {
        var world = ctx.World.Raw;
        _toQueue.Clear();
        _toRespawn.Clear();

        var pending = new QueryDescription().WithAll<KnockedOut, RespawnLives>();
        world.Query(in pending, (Entity e) =>
        {
            if (world.Has<RespawnTimer>(e))
            {
                ref var timer = ref world.Get<RespawnTimer>(e);
                if (timer.TicksRemaining > 0)
                {
                    timer.TicksRemaining--;
                }

                if (timer.TicksRemaining == 0)
                {
                    _toRespawn.Add(e);
                }

                return;
            }

            ref var lives = ref world.Get<RespawnLives>(e);
            if (lives.Remaining == 0)
            {
                return;
            }

            _toQueue.Add(e);
        });

        foreach (var entity in _toQueue)
        {
            ref var lives = ref world.Get<RespawnLives>(entity);
            lives.Remaining--;
            world.Add(entity, new RespawnTimer { TicksRemaining = _respawnDelayTicks });
        }

        foreach (var entity in _toRespawn)
        {
            ref var lives = ref world.Get<RespawnLives>(entity);
            ref var transform = ref world.Get<Transform>(entity);
            transform.Position = lives.SpawnPosition;
            transform.YawRadians = lives.SpawnYawRadians;

            if (world.Has<Hull>(entity))
            {
                ref var hull = ref world.Get<Hull>(entity);
                hull.DynamicsState = Opus.Engine.Physics.Ground.GroundVehicleState.Rest(
                    lives.SpawnPosition,
                    lives.SpawnYawRadians);
            }

            if (world.Has<DriveInput>(entity))
            {
                world.Set(entity, new DriveInput { Throttle = 0f, Steering = 0f });
            }

            if (world.Has<TurretTarget>(entity))
            {
                world.Set(entity, new TurretTarget { YawRadians = lives.SpawnYawRadians });
            }

            world.Remove<RespawnTimer>(entity);
            world.Remove<KnockedOut>(entity);

            // Spawn invulnerability — the returning crew gets a brief shielded window so
            // a spawn-camper cannot one-shot it. Zero ticks opts out (single-player /
            // determinism scenarios keep the legacy no-shield behaviour).
            if (_spawnInvulnerabilityTicks > 0)
            {
                world.Add(entity, new RespawnInvulnerable
                {
                    TicksRemaining = _spawnInvulnerabilityTicks,
                });
            }
        }
    }
}
