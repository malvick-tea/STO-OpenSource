using System;
using System.Numerics;
using Garupan.Content;
using Garupan.Sim.Components;
using Garupan.Sim.Spawn;

namespace Garupan.Sim.Replay;

/// <summary>
/// Canonical determinism scenarios — the replay-matrix tests run each at a fixed tick
/// count and assert the recorded byte stream's SHA-256 matches a pinned constant. Each
/// scenario is a pure-function world builder (takes a fresh <see cref="World"/>, spawns
/// the canonical entities, returns void); the harness owns the pipeline + writer.
/// </summary>
/// <remarks>
/// Add a scenario by appending a static builder here and a matching pinned hash in
/// <c>ReplayMatrixTests</c>. The builder must be referentially transparent: it must not
/// depend on wall-clock state, ambient randomness, or order of static-field
/// initialisation; identical input world == identical output entities.
/// </remarks>
public static class DeterminismScenarios
{
    /// <summary>1v1 VehicleMediumB (player) vs VehicleHeavyA (AI). Symmetric facing on the
    /// y-axis, centred at origin. The original canonical determinism scenario.</summary>
    public static void BuildSinglePair(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumB,
            new Vector2(0f, -30f),
            yawRadians: MathF.PI / 2f,
            Team.PlayerSchool,
            TankControl.Player);

        TankSpawner.Spawn(
            world,
            TankRoster.VehicleHeavyA,
            new Vector2(0f, 30f),
            yawRadians: -MathF.PI / 2f,
            Team.OpponentSchool,
            TankControl.AiBot);
    }

    /// <summary>1v3 multi-opponent: player VehicleMediumB facing forward at origin, with a
    /// VehicleHeavyA anchor north-east and two VehicleMediumB wingmen flanking off-axis. Mirrors
    /// the canonical demo composition (see <c>data/garage-demo-match.csv</c>).</summary>
    public static void BuildMultiOpponent(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumB,
            Vector2.Zero,
            yawRadians: 0f,
            Team.PlayerSchool,
            TankControl.Player);

        TankSpawner.Spawn(
            world,
            TankRoster.VehicleHeavyA,
            new Vector2(18f, 18f),
            yawRadians: MathF.PI,
            Team.OpponentSchool,
            TankControl.AiBot);

        TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumB,
            new Vector2(-15f, 22f),
            yawRadians: -3f * MathF.PI / 4f,
            Team.OpponentSchool,
            TankControl.AiBot);

        TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumB,
            new Vector2(20f, -8f),
            yawRadians: 3f * MathF.PI / 4f,
            Team.OpponentSchool,
            TankControl.AiBot);
    }

    /// <summary>1v2 at very close range — the player's medium tank vs a heavy tank and a
    /// medium tank at 15 m distance, all three already aligned. Exercises the
    /// projectile / hit-resolve path on tick ~3-6 (one heavy-tank shot vs the medium tank's
    /// side armour), producing a knockout mid-replay. Pins the entire knockout-state
    /// flow + tank death cascade.</summary>
    public static void BuildPointBlankExchange(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumB,
            Vector2.Zero,
            yawRadians: 0f,
            Team.PlayerSchool,
            TankControl.Player);

        TankSpawner.Spawn(
            world,
            TankRoster.VehicleHeavyA,
            new Vector2(15f, 0f),
            yawRadians: MathF.PI,
            Team.OpponentSchool,
            TankControl.AiBot);

        TankSpawner.Spawn(
            world,
            TankRoster.VehicleMediumC,
            new Vector2(-15f, 5f),
            yawRadians: 0f,
            Team.OpponentSchool,
            TankControl.AiBot);
    }
}
