namespace Garupan.Sim.Snapshot;

/// <summary>
/// Outcome of <see cref="SnapshotDecoder.TryDecode"/>. Mirrors C++ <c>CodecResult</c>:
/// <see cref="Error"/> is a literal string (no allocation in the hot path) so callers can
/// log and route without copying. <see cref="Ok"/> false plus a null Error is reserved for
/// "decode not yet attempted" sentinel use; runtime paths always set both.
/// </summary>
public readonly record struct SnapshotCodecResult(bool Ok, string? Error)
{
    public static SnapshotCodecResult Success { get; } = new(true, null);

    public static SnapshotCodecResult Fail(string error) => new(false, error);
}
