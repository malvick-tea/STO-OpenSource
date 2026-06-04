using System.Numerics;

namespace Garupan.Sim.Components;

/// <summary>
/// Per-tank respawn budget for multi-life match modes. Carries the number of respawns the
/// peer has remaining plus the spawn anchor used to restore the tank's transform.
/// Attached at <see cref="Spawn.TankSpawner.Spawn"/> when the caller passes a positive
/// respawn-count; omitted entirely for matches without respawns (single-player canon
/// missions, the determinism replay-matrix, the legacy Phase-0 server default).
/// </summary>
/// <remarks>
/// <para>
/// The local test modes ([[garupan-local test-2026]]) set this per-mode:
/// <list type="bullet">
/// <item><description>Hungry Battles (10v10 FFA) — Remaining = 3 (three respawns + the initial spawn = four lives total).</description></item>
/// <item><description>Tactical 5v5 — Remaining = 1 (one respawn + the initial spawn = two lives total).</description></item>
/// </list>
/// </para>
/// <para>
/// <see cref="RespawnSystem"/> decrements <see cref="Remaining"/> the first tick a
/// <see cref="KnockedOut"/> tag arrives, then queues a <see cref="RespawnTimer"/> for the
/// delay. The system reads <see cref="SpawnPosition"/> + <see cref="SpawnYawRadians"/> to
/// reset the tank's <see cref="Transform"/> when the timer expires — storing the anchor
/// on the entity itself keeps the system fully Sim-tier (no per-match-host lookup).
/// </para>
/// </remarks>
public struct RespawnLives
{
    /// <summary>Respawns left, decremented each time the tank is knocked out. Hits zero
    /// when the next knock-out is final and the tank stays a wreck on the field.</summary>
    public byte Remaining;

    /// <summary>Initial spawn anchor in metres (X, Z). Reused as the respawn target so
    /// the player rejoins where they started — keeps the local test matches readable
    /// without per-mode spawn-rotation logic.</summary>
    public Vector2 SpawnPosition;

    /// <summary>Initial hull yaw at spawn (radians, world frame). Restored on respawn.</summary>
    public float SpawnYawRadians;
}
