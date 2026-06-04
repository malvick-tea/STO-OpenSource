using System;
using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Garupan.Content;
using Garupan.Sim.Collision;
using Garupan.Sim.Components;
using Opus.Engine.Physics.Destruction;
using Opus.Engine.Physics.Ground;

namespace Garupan.Sim.Systems;

/// <summary>
/// Resolves tanks against destructible map props. A standing prop is a solid obstacle: a hull
/// that touches one either defeats it — felling a trunk or smashing a sign — or is stopped by it.
/// Defeat needs enough <em>energy</em> (a charging hull) OR enough <em>force</em> (a strong hull
/// leaning on it from rest); both thresholds were stamped from the prop's size + material at spawn
/// (<see cref="Spawn.MapPropSpawner"/>), so the whole roster and the whole city's clutter interact
/// through physics with no per-pair tuning. Below both thresholds the prop blocks the hull —
/// pushing it back out and cancelling the velocity driving into the obstacle.
/// </summary>
/// <remarks>
/// Order 350 — after the hull has been integrated to its new position (300), before aim (400), so
/// contacts resolve where the tank actually ended up. A felled prop ages from
/// <see cref="PropState.Toppling"/> to <see cref="PropState.Fallen"/> over
/// <see cref="ToppleDurationSeconds"/>; a broken one is inert at once. The broad phase is the naive
/// tank×prop scan — fine for the present prop counts; a spatial grid is the documented follow-up.
/// </remarks>
public sealed class PropCollisionSystem : IFixedSystem
{
    /// <summary>Pipeline position: after hull integration (300), before aim (400).</summary>
    public const int PipelineOrder = 350;

    /// <summary>Seconds a felled prop spends hinging over before it lies flat — the single source of
    /// truth for the topple timeline, consumed by the client's prop-box hinge animation.</summary>
    public const float ToppleDurationSeconds = 0.8f;

    private const float DegenerateContactEpsilon = 1e-4f;

    /// <summary>Throttle magnitude below which a hull is not driving, so it lends no static push to
    /// an obstacle — a parked tank does not fell trees, only a charging or flooring one does.</summary>
    private const float MinimumPushThrottle = 0.05f;

    private static readonly float SurfaceFrictionCoefficient =
        GroundVehicleEnvironment.EarthCompactedGround.Surface.LongitudinalFrictionCoefficient;

    private static readonly float GravityMps2 = GroundVehicleEnvironment.EarthCompactedGround.GravityMps2;

    /// <summary>Reused between ticks so the standing-prop scan does not allocate per frame.</summary>
    private readonly List<PropContact> _standing = new();

    public string Name => "PropCollision";

    public int Order => PipelineOrder;

    public void Tick(in TickContext ctx)
    {
        var dt = MathF.Max(0f, (float)ctx.Time.TickIntervalSeconds);
        var world = ctx.World;
        AgeFellingProps(world, dt);
        ResolveTankContacts(world);
    }

    /// <summary>Ages props mid-topple into their settled fallen state.</summary>
    private static void AgeFellingProps(World world, float dt)
    {
        var query = new QueryDescription().WithAll<DestructibleProp>();
        world.Raw.Query(in query, (ref DestructibleProp prop) =>
        {
            if (prop.State != PropState.Toppling)
            {
                return;
            }

            prop.StateSeconds += dt;
            if (prop.StateSeconds >= ToppleDurationSeconds)
            {
                prop.State = PropState.Fallen;
            }
        });
    }

    private void ResolveTankContacts(World world)
    {
        _standing.Clear();
        CollectStandingProps(world);
        if (_standing.Count == 0)
        {
            return;
        }

        var standing = _standing;
        var tankQuery = new QueryDescription().WithAll<Transform, Hull, HitRadius, DriveInput>().WithNone<KnockedOut>();
        world.Raw.Query(in tankQuery, (ref Transform tf, ref Hull hull, ref HitRadius radius, ref DriveInput input) =>
        {
            if (hull.Dynamics is not { } dynamics)
            {
                return;
            }

            // The hull's traction-limited drive force is only available when it is actually
            // throttling; otherwise it can still defeat a prop by charging momentum, not by leaning.
            var pushForce = MathF.Abs(input.Throttle) > MinimumPushThrottle
                ? SurfaceFrictionCoefficient * dynamics.MassKg * GravityMps2 * dynamics.TractionScale
                : 0f;

            for (var i = 0; i < standing.Count; i++)
            {
                var contact = standing[i];
                if (contact.Data.State == PropState.Standing
                    && Resolve(ref tf, ref hull, radius.Meters, dynamics.MassKg, pushForce, ref contact))
                {
                    standing[i] = contact; // a felled prop must read as defeated for later tanks this tick
                }
            }
        });

        WriteBackDefeated(world);
    }

    private void CollectStandingProps(World world)
    {
        var query = new QueryDescription().WithAll<Transform, DestructibleProp>();
        var standing = _standing;
        world.Raw.Query(in query, (Entity entity, ref Transform tf, ref DestructibleProp prop) =>
        {
            if (prop.State == PropState.Standing)
            {
                standing.Add(new PropContact(new EntityHandle(entity), tf.Position, prop));
            }
        });
    }

    /// <summary>Resolves one tank against one standing prop. Returns true only when the prop is
    /// defeated (so the caller persists the new state); a block mutates the tank refs in place.</summary>
    private static bool Resolve(
        ref Transform tf,
        ref Hull hull,
        float tankRadiusMeters,
        float massKg,
        float pushForceNewtons,
        ref PropContact contact)
    {
        var offset = contact.Position - tf.Position;
        var distance = offset.Length();
        var contactDistance = tankRadiusMeters + contact.Data.RadiusMeters;
        if (distance >= contactDistance || distance < DegenerateContactEpsilon)
        {
            return false;
        }

        var toward = offset / distance;
        var velocity = hull.DynamicsState.VelocityMps;
        var approachSpeed = MathF.Max(0f, Vector2.Dot(velocity, toward));
        var impactEnergy = PropResistance.KineticEnergyJoules(massKg, approachSpeed);

        if (impactEnergy >= contact.Data.ToppleEnergyJoules || pushForceNewtons >= contact.Data.ResistingForceNewtons)
        {
            SpendDefeatEnergy(ref hull, toward, massKg, approachSpeed, contact.Data.ToppleEnergyJoules);
            Fell(ref contact, toward);
            return true;
        }

        // The prop holds: push the hull back out (away from the prop = −toward) and cancel the
        // velocity driving into it, so the tank stalls against it instead of clipping through.
        HullContactResponse.Separate(ref tf, ref hull, -toward, contactDistance - distance);
        return false;
    }

    /// <summary>Converts the obstacle's defeat work into a loss of approach velocity. The lateral
    /// component is untouched: the prop consumes only the kinetic energy delivered into the contact,
    /// so heavier/faster tanks and tougher props scale through the same equation.</summary>
    private static void SpendDefeatEnergy(
        ref Hull hull,
        Vector2 toward,
        float massKg,
        float approachSpeed,
        float energyJoules)
    {
        if (massKg <= 0f || approachSpeed <= 0f || energyJoules <= 0f)
        {
            return;
        }

        var impactEnergy = PropResistance.KineticEnergyJoules(massKg, approachSpeed);
        var remainingEnergy = MathF.Max(0f, impactEnergy - energyJoules);
        var remainingApproachSpeed = MathF.Sqrt(2f * remainingEnergy / massKg);
        var spentSpeed = approachSpeed - remainingApproachSpeed;
        if (spentSpeed <= 0f)
        {
            return;
        }

        var velocity = hull.DynamicsState.VelocityMps;
        hull.DynamicsState = hull.DynamicsState with
        {
            VelocityMps = velocity - (toward * spentSpeed),
        };
    }

    /// <summary>The prop is defeated: a rooted member hinges over (Toppling), brittle clutter
    /// shatters (Broken). The fall heads the way the impactor was travelling.</summary>
    private static void Fell(ref PropContact contact, Vector2 toward)
    {
        contact.Data.State = contact.Data.Behavior == PropBehavior.Topple ? PropState.Toppling : PropState.Broken;
        contact.Data.FallYawRadians = MathF.Atan2(toward.Y, toward.X);
        contact.Data.StateSeconds = 0f;
        contact.Defeated = true;
    }

    private void WriteBackDefeated(World world)
    {
        for (var i = 0; i < _standing.Count; i++)
        {
            if (_standing[i].Defeated)
            {
                world.Set(_standing[i].Handle, _standing[i].Data);
            }
        }
    }

    private struct PropContact
    {
        public PropContact(EntityHandle handle, Vector2 position, DestructibleProp data)
        {
            Handle = handle;
            Position = position;
            Data = data;
            Defeated = false;
        }

        public EntityHandle Handle { get; }

        public Vector2 Position { get; }

        public DestructibleProp Data;

        public bool Defeated;
    }
}
