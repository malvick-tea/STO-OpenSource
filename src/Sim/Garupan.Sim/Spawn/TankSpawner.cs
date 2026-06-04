using System.Numerics;
using Garupan.Content;
using Garupan.Sim.Components;
using Opus.Foundation;

namespace Garupan.Sim.Spawn;

/// <summary>
/// Builds a fully-equipped tank entity from a <see cref="TankSpec"/>. The single
/// sanctioned bridge between the static catalogue (Opus.Content) and the live ECS
/// world: every other call site that needs a tank in the world goes through here.
///
/// Orchestration only — the per-fragment construction lives in:
/// <list type="bullet">
/// <item><description><see cref="SpecConversion"/> — units + Phase-0 ratios.</description></item>
/// <item><description><see cref="ChassisBuilder"/> — Hull + ArmorMap.</description></item>
/// <item><description><see cref="TurretGunBuilder"/> — Turret + Gun + ChamberedRound via <see cref="AmmoCatalog"/>.</description></item>
/// <item><description><see cref="ControllerStamper"/> — Player / AI / inert tag routing.</description></item>
/// </list>
///
/// Components attached on success:
/// <list type="bullet">
/// <item><description><see cref="Transform"/> — position + hull yaw from arguments.</description></item>
/// <item><description><see cref="Hull"/>, <see cref="Turret"/>, <see cref="Gun"/> from the builders above.</description></item>
/// <item><description><see cref="HitRadius"/> — Phase-0 bounding-circle (single constant for every chassis).</description></item>
/// <item><description><see cref="TeamTag"/>, <see cref="DriveInput"/>, <see cref="TurretTarget"/> — zeroed so the
///     first controller tick overwrites them.</description></item>
/// <item><description><see cref="PlayerControlled"/> / <see cref="AiControlled"/> + <see cref="BotBrain"/> — per <see cref="TankControl"/>.</description></item>
/// <item><description><see cref="NetworkId"/> — optional, stamped only when the caller supplies one
///     (the authoritative match host does; single-player / determinism spawns omit it).</description></item>
/// </list>
///
/// Ported from <c>svo/engine/src/spawn.cpp</c>.
/// </summary>
public static class TankSpawner
{
    /// <summary>Re-exported from <see cref="ControllerStamper"/> for call-sites that already
    /// reach for the spawner namespace.</summary>
    public const float DefaultBotEngageRangeMeters = ControllerStamper.DefaultBotEngageRangeMeters;

    public static EntityHandle Spawn(
        World world,
        TankSpec spec,
        Vector2 position,
        float yawRadians,
        Team team,
        TankControl control,
        BotBrain? botBrain = null,
        NetworkId? networkId = null,
        byte respawnLives = 0)
    {
        Ensure.NotNull(world);
        Ensure.NotNull(spec);

        var hull = ChassisBuilder.Build(spec);
        var turret = TurretGunBuilder.BuildTurret(spec, yawRadians);
        var gun = TurretGunBuilder.BuildGun(spec.Gun);
        var gunMount = TurretGunBuilder.BuildGunMount(spec.GunMount);

        var entity = world.Spawn(new Transform(position, yawRadians), hull, turret);
        world.Add(entity, gun);
        world.Add(entity, gunMount);
        world.Add(entity, new HitRadius { Meters = ChassisBuilder.HitRadiusMeters(spec) });
        world.Add(entity, ChassisBuilder.Silhouette(spec));
        world.Add(entity, new TeamTag { Team = team });
        world.Add(entity, new DriveInput { Throttle = 0f, Steering = 0f });
        world.Add(entity, new TurretTarget { YawRadians = yawRadians });

        ControllerStamper.Stamp(world, entity, control, botBrain);
        if (networkId is { } id)
        {
            world.Add(entity, id);
        }

        if (respawnLives > 0)
        {
            world.Add(entity, new RespawnLives
            {
                Remaining = respawnLives,
                SpawnPosition = position,
                SpawnYawRadians = yawRadians,
            });
        }

        return entity;
    }
}

/// <summary>
/// Controller affiliation stamped onto a freshly-spawned tank. Keeps
/// <see cref="TankSpawner.Spawn"/> a single call instead of forcing every caller to
/// remember which tag goes with which kind of tank.
/// </summary>
public enum TankControl
{
    /// <summary>No controller tag. The tank sits inert until something else writes
    /// <see cref="DriveInput"/> / <see cref="TurretTarget"/> directly.</summary>
    None = 0,

    /// <summary>Stamps <see cref="PlayerControlled"/>. The input adapter writes DriveInput
    /// / TurretTarget / FireIntent each frame.</summary>
    Player = 1,

    /// <summary>Stamps <see cref="AiControlled"/> and <see cref="BotBrain"/>. The AI loop
    /// drives the tank from then on.</summary>
    AiBot = 2,
}
