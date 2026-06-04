namespace Garupan.Sim;

/// <summary>
/// 64-bit deterministic seed identifying a simulation run. Hash of (mission_id,
/// replay_nonce, player_profile_id) — same triple always produces the same seed,
/// and the same seed always produces a bit-identical replay.
///
/// Stored as a value type so it crosses the Sim → Persistence boundary by-value
/// without any reference-equality footguns.
/// </summary>
public readonly record struct SimSeed(ulong Value)
{
    public static SimSeed Zero => new(0UL);

    public override string ToString() => $"seed#{Value:x16}";
}
