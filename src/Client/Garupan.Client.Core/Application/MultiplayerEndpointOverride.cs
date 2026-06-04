using MemoryPack;

namespace Garupan.Client.Core.Application;

/// <summary>
/// Per-mode override of the default <see cref="MultiplayerSettings"/> endpoint. An empty
/// <see cref="Host"/> (whitespace or default) plus a zero <see cref="Port"/> mean
/// "no override — fall back to the default endpoint"; a populated override wins over
/// the default for matches of the bound mode.
/// </summary>
/// <remarks>
/// <para>
/// Why split into a record: the local test dev hosts one server per mode on different
/// ports (per <c>Garupan.Server.Console --mode</c>); a tester then picks the matching
/// lobby card and dials the right endpoint without having to retype it. A single
/// <see cref="MultiplayerSettings"/> snapshot carries up to two overrides (Hungry Battles
/// + Tactical 5v5) — keeping the record dedicated avoids a god-object settings shape.
/// </para>
/// <para>
/// Sentinel encoding: an empty host string + a zero port is the "no override" reading.
/// The resolver checks <see cref="IsConfigured"/> instead of a nullable record, which
/// keeps the MemoryPack frame purely positional + avoids a null-vs-default ambiguity in
/// round-trip tests.
/// </para>
/// </remarks>
[MemoryPackable]
public sealed partial record MultiplayerEndpointOverride
{
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; }

    /// <summary>The default "no override" value — empty host, zero port. The resolver
    /// reads this as "fall back to the default endpoint".</summary>
    public static MultiplayerEndpointOverride None => new();

    /// <summary>True when both host and port are populated to legal values — the
    /// resolver should prefer this over the default. Anything less (empty host, zero
    /// port, out-of-range port) reads as no override.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host)
        && Port >= MultiplayerSettings.MinPort
        && Port <= MultiplayerSettings.MaxPort;
}
