namespace Garupan.Sim.Protocol;

/// <summary>
/// Outcome of a decode call. Mirrors the C++ codec result shape — <see cref="Error"/> is
/// a literal string so the codec hot path allocates nothing on the failure branch.
///
/// Sits in the Protocol namespace because every wire codec (welcome, client input,
/// snapshot) uses the same shape; consolidating it here keeps a future "framing layer"
/// from having to translate between three near-identical types.
/// </summary>
public readonly record struct WireCodecResult(bool Ok, string? Error)
{
    public static WireCodecResult Success { get; } = new(true, null);

    public static WireCodecResult Fail(string error) => new(false, error);
}
