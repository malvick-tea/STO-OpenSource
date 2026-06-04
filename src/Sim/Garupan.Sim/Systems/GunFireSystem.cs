using System;
using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Garupan.Sim.Components;
using Garupan.Sim.Spawn;
using Opus.Engine.Physics.Ballistics;

namespace Garupan.Sim.Systems;

/// <summary>
/// Spawns a projectile entity for each tank that has a <see cref="FireIntent"/> AND a
/// loaded gun AND isn't <see cref="KnockedOut"/>. The intent component is removed
/// regardless — a "trigger held with no gun" or a "fire on cooldown" both end clean.
///
/// World-frame aim: turret yaw is already authoritative world yaw. The projectile starts
/// at the gun muzzle and launches along yaw plus barrel pitch. Opus exterior ballistics
/// derives its drag, gravity arc, time of flight, and travelled distance.
///
/// Firing drives the gun assembly backward. <see cref="ApplyRecoil"/> resolves the shell,
/// propellant gas, muzzle brake, recoil mechanism, hull mass, and ground grip. Only momentum
/// that the recoil mechanism and static friction cannot absorb moves the chassis.
///
/// Cooldown after fire: gun.ReloadSeconds := gun.ReloadSecondsMax. <see cref="ReloadTickSystem"/>
/// counts it down on subsequent ticks.
///
/// Order: 470 — runs after existing projectiles integrate, before hit-resolve.
/// Ported from <c>svo/engine/src/systems/gun_fire.cpp</c>.
/// </summary>
public sealed class GunFireSystem : IFixedSystem
{
    /// <summary>
    /// Safety lifetime for rounds that never meet terrain due to malformed world data.
    /// Normal projectiles terminate at a physical ground impact before this budget.
    /// </summary>
    public const float ProjectileLifetimeSeconds = 30f;

    public string Name => "GunFire";

    public int Order => 470;

    public void Tick(in TickContext ctx)
    {
        var world = ctx.World;
        var raw = world.Raw;

        // Snapshot intents up-front. Iterating the view while creating new projectile
        // entities (which themselves carry Transform) can in principle invalidate
        // Arch's chunk pointers; collecting first into a small list makes the loop
        // safe regardless of how the registry chooses to grow its pools.
        var shooters = new List<Entity>();
        var intentQuery = new QueryDescription().WithAll<FireIntent>();
        raw.Query(in intentQuery, (Entity e) => shooters.Add(e));

        foreach (var rawShooter in shooters)
        {
            FireOne(world, raw, rawShooter);
        }
    }

    /// <summary>Resolves one shooter's fire intent: clears the intent, and — when the gun is
    /// loaded, chambered, and the hull is alive — spawns the round at the muzzle, resets the
    /// reload clock, and recoils the hull.</summary>
    private static void FireOne(Garupan.Sim.World world, Arch.Core.World raw, Entity rawShooter)
    {
        var shooter = new EntityHandle(rawShooter);

        // Intent leaves regardless of whether we actually fire.
        world.Remove<FireIntent>(shooter);

        if (!raw.Has<Gun>(rawShooter)
            || !raw.Has<Transform>(rawShooter)
            || !raw.Has<Turret>(rawShooter)
            || !raw.Has<GunMount>(rawShooter))
        {
            return;
        }

        if (raw.Has<KnockedOut>(rawShooter))
        {
            return;
        }

        // Copy values into locals BEFORE the Spawn — adding components to a new entity can
        // shift Arch chunk pointers, invalidating any refs taken into the shooter's archetype.
        // Read once, write back once.
        var gunSnapshot = raw.Get<Gun>(rawShooter);
        if (gunSnapshot.ReloadSeconds > 0f)
        {
            return; // on cooldown — silent skip
        }

        if (gunSnapshot.Chambered.MuzzleVelocityMps <= 0f)
        {
            return; // no chambered round — silent skip
        }

        var shooterTf = raw.Get<Transform>(rawShooter);
        var shooterTurret = raw.Get<Turret>(rawShooter);
        var shooterMount = raw.Get<GunMount>(rawShooter);
        var hasHull = raw.Has<Hull>(rawShooter);
        var hull = hasHull ? raw.Get<Hull>(rawShooter) : default;

        var worldAimYaw = shooterTurret.YawRadians;
        var muzzlePosition = shooterMount.MuzzlePosition(
            shooterTf.Position,
            worldAimYaw,
            shooterTurret.BarrelPitchRadians);
        var muzzleHeight = shooterMount.MuzzleHeightMeters(shooterTurret.BarrelPitchRadians);
        var velocity = BallisticLaunch.Velocity(
            gunSnapshot.Chambered.MuzzleVelocityMps,
            worldAimYaw,
            shooterTurret.BarrelPitchRadians);
        var dynamics = BuildDynamics(gunSnapshot.Chambered);

        var projectile = world.Spawn(
            new Transform(muzzlePosition, worldAimYaw),
            new Projectile
            {
                Velocity = new Vector2(velocity.X, velocity.Z),
                VerticalVelocityMps = velocity.Y,
                MuzzleVelocityMps = gunSnapshot.Chambered.MuzzleVelocityMps,
                VisualHeightMeters = muzzleHeight,
                LaunchPosition = muzzlePosition,
                LaunchVisualHeightMeters = muzzleHeight,
                Dynamics = dynamics,
                MassKg = gunSnapshot.Chambered.MassKg,
                Penetration = gunSnapshot.Chambered.Penetration,
                Type = gunSnapshot.Chambered.Type,
            });

        world.Add(projectile, new Lifetime { SecondsRemaining = ProjectileLifetimeSeconds });
        world.Add(projectile, new Owner { Entity = shooter });

        // Reset cooldown clock and write back. ReloadTickSystem counts down next tick.
        gunSnapshot.ReloadSeconds = gunSnapshot.ReloadSecondsMax;
        world.Set(shooter, gunSnapshot);

        if (hasHull)
        {
            ApplyRecoil(world, shooter, hull, gunSnapshot, worldAimYaw, shooterTurret.BarrelPitchRadians);
        }
    }

    /// <summary>Resolves Newton's third law through the gun mount and the ground contact patch.
    /// The recoiling assembly always animates; the hull moves only when its available static
    /// friction cannot hold the residual horizontal recoil impulse.</summary>
    private static void ApplyRecoil(
        Garupan.Sim.World world,
        EntityHandle shooter,
        Hull hull,
        Gun gun,
        float aimYaw,
        float barrelPitchRadians)
    {
        if (hull.Dynamics is not { } dynamics || dynamics.MassKg <= 0f)
        {
            return;
        }

        var round = gun.Chambered;
        var ground = Opus.Engine.Physics.Ground.GroundVehicleEnvironment.EarthCompactedGround;
        var response = GunRecoil.Solve(new GunRecoilProperties(
            round.MassKg,
            round.MuzzleVelocityMps,
            round.PropellantChargeMassKg,
            round.GasVelocityFactor,
            gun.MuzzleBrakeEfficiency,
            barrelPitchRadians,
            gun.RecoilingAssemblyMassKg,
            gun.MaximumRecoilTravelMeters,
            gun.RecoilBrakeForceNewtons,
            dynamics.MassKg,
            ground.Surface.LongitudinalFrictionCoefficient * dynamics.TractionScale,
            ground.GravityMps2));
        var recoilDirection = -new Vector2(MathF.Cos(aimYaw), MathF.Sin(aimYaw));
        hull.DynamicsState = hull.DynamicsState with
        {
            VelocityMps = hull.DynamicsState.VelocityMps
                + (recoilDirection * response.PlatformSpeedChangeMetersPerSecond),
        };
        world.Set(shooter, hull);
        if (response.RecoilTravelMeters <= 0f || gun.RecoilReturnSeconds <= 0f)
        {
            return;
        }

        world.AddOrSet(shooter, new GunRecoilState
        {
            TravelMeters = response.RecoilTravelMeters,
            ReturnSpeedMetersPerSecond = response.RecoilTravelMeters / gun.RecoilReturnSeconds,
        });
    }

    private static BallisticBodyProperties BuildDynamics(ChamberedRound chambered)
    {
        return BallisticBodyProperties.FromDiameter(
            chambered.MassKg,
            chambered.DiameterMeters,
            new ConstantDragCoefficientCurve(chambered.DragCoefficient));
    }
}
