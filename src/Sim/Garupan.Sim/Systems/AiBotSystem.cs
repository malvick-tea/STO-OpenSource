using System;
using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Garupan.Sim.Components;

namespace Garupan.Sim.Systems;

/// <summary>
/// Simple Phase-0 enemy AI: pick the nearest in-range enemy, aim turret at them, fire when
/// loaded, drive toward them at half-speed. Crude but enough to make a Prefectural
/// match playable. Runtime AI lands per-school in M5+ via the per-school personality
/// modules described in ADR-0008 (RivalDelta's siege patience, RivalEcho's overwhelming
/// firepower, etc.).
///
/// Engagement gating reads <see cref="BotBrain.EngageRangeMeters"/> off the entity, so
/// per-tank / per-school tuning is a data change. Phase-0 default (60 m) matches the C++
/// reference; later milestones widen this for snipers, shrink for brawlers.
///
/// Order: 200 — runs after ApplyInputs (100), before HullDrive (300).
/// Ported from <c>svo/engine/src/systems/ai_bot.cpp</c>.
/// </summary>
public sealed class AiBotSystem : IFixedSystem
{
    /// <summary>Cruise throttle for AI in Phase 0. Real AI varies based on personality.</summary>
    private const float AiThrottle = 0.5f;

    /// <summary>Don't fire if turret hasn't aligned to within this many radians of target yaw.</summary>
    private const float FireAlignmentTolerance = 0.05f; // ~3°

    public string Name => "AiBot";

    public int Order => 200;

    public void Tick(in TickContext ctx)
    {
        var raw = ctx.World.Raw;
        var world = ctx.World;

        // Snapshot positions + teams of all candidate targets first. AI loop reads this
        // to find nearest enemy without triggering more queries per AI tank.
        var allTanks = new List<(Entity Entity, Vector2 Pos, Team Team)>();
        var snapshotQuery = new QueryDescription()
            .WithAll<Transform, Hull, TeamTag>()
            .WithNone<KnockedOut>();
        raw.Query(in snapshotQuery, (Entity e, ref Transform tf, ref TeamTag team) =>
        {
            allTanks.Add((e, tf.Position, team.Team));
        });

        var aiQuery = new QueryDescription()
            .WithAll<Transform, Hull, Turret, Gun, TeamTag, AiControlled, BotBrain>()
            .WithNone<KnockedOut>();

        var fireList = new List<EntityHandle>();

        raw.Query(
            in aiQuery,
            (Entity e, ref Transform tf, ref Hull hull, ref Turret turret, ref Gun gun, ref TeamTag team, ref BotBrain brain) =>
        {
            // Find nearest enemy inside engage range. Squared distance keeps the inner
            // loop sqrt-free; the cutoff is the squared engage radius so a target right
            // at the edge is included.
            var engageRangeSq = brain.EngageRangeMeters * brain.EngageRangeMeters;
            var bestDist = engageRangeSq;
            var bestPos = Vector2.Zero;
            var found = false;
            for (var i = 0; i < allTanks.Count; i++)
            {
                var candidate = allTanks[i];
                if (candidate.Entity == e || candidate.Team == team.Team || candidate.Team == Team.None)
                {
                    continue;
                }

                var d = Vector2.DistanceSquared(tf.Position, candidate.Pos);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestPos = candidate.Pos;
                    found = true;
                }
            }

            if (!found)
            {
                // No reachable enemies — idle, don't carry over previous tick's throttle.
                world.AddOrSet(new EntityHandle(e), new DriveInput { Throttle = 0f, Steering = 0f });
                return;
            }

            // Aim turret at enemy (world-frame yaw, matching the Phase-0 TurretTarget
            // convention in TurretAimSystem).
            var aimVec = bestPos - tf.Position;
            var targetYaw = MathF.Atan2(aimVec.Y, aimVec.X);
            world.AddOrSet(new EntityHandle(e), new TurretTarget { YawRadians = targetYaw });

            // Drive toward enemy at cruise throttle, steer hull toward the bearing.
            var bearing = WrapSignedPi(targetYaw - tf.YawRadians);
            var steering = MathF.Sign(bearing) * MathF.Min(1f, MathF.Abs(bearing) * 2f);
            world.AddOrSet(new EntityHandle(e), new DriveInput { Throttle = AiThrottle, Steering = steering });

            // Fire if loaded AND turret roughly aligned with target.
            if (gun.ReloadSeconds <= 0f && MathF.Abs(WrapSignedPi(targetYaw - turret.YawRadians)) < FireAlignmentTolerance)
            {
                fireList.Add(new EntityHandle(e));
            }
        });

        // Apply FireIntents after the query loop so we don't mutate during iteration.
        foreach (var handle in fireList)
        {
            world.Add(handle, default(FireIntent));
        }
    }

    private static float WrapSignedPi(float angle)
    {
        const float pi = MathF.PI;
        const float twoPi = 2f * MathF.PI;
        var shifted = angle + pi;
        var folded = shifted - (MathF.Floor(shifted / twoPi) * twoPi);
        return folded - pi;
    }
}
