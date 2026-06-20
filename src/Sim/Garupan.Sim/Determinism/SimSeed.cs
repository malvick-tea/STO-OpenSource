namespace Garupan.Sim;

/// <summary>
/// 64-bit deterministic seed identifying a simulation run. The caller owns seed
/// derivation; equal seeds and equal inputs must produce bit-identical simulation output.
///
/// Stored as a value type so it crosses the simulation and persistence boundary by value
/// without reference-identity ambiguity.
/// </summary>
public readonly record struct SimSeed(ulong Value)
{
    public static SimSeed Zero => new(0UL);

    public override string ToString() => $"seed#{Value:x16}";
}
