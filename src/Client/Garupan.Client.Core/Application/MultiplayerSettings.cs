using MemoryPack;

namespace Garupan.Client.Core.Application;

/// <summary>
/// Player-configurable local test match server target. Nested inside
/// <see cref="AppSettings"/> so it rides the same <c>user://settings.gsav</c> frame as
/// volume, locale, and bindings — server address is a user preference, not a separate
/// save kind. Adding a field here is a schema change — bump
/// <see cref="SaveSchemas.Settings"/>.
/// </summary>
/// <remarks>
/// <para>
/// The host is stored as a free-form string (an IPv4 / IPv6 literal, or a DNS name —
/// either works at this layer); the port is a 16-bit unsigned value but stored as an
/// <see cref="int"/> for ergonomic clamp arithmetic on the settings UI. Resolution to a
/// concrete <c>IPEndPoint</c> happens in <c>NetworkMatchEndpointResolver</c>.
/// </para>
/// <para>
/// Default <c>127.0.0.1 : 7777</c> — matches the <c>NetworkMatchEndpoint.DefaultPort</c>
/// and <c>ServerConsoleOptionsParser.DefaultPort</c> literals so a dev box running both
/// halves connects with zero config. A local test tester runs the multiplayer
/// sub-screen to point this at the dev-hosted server's public IP.
/// </para>
/// </remarks>
[MemoryPackable]
public sealed partial record MultiplayerSettings
{
    /// <summary>Maximum legal host string length. RFC 1035 caps a DNS name at 253 octets;
    /// 255 leaves room for the trailing dot some tools keep, and is a clean cap for an
    /// IPv6 literal (45 chars max) or any reasonable hostname.</summary>
    public const int MaxHostLength = 255;

    /// <summary>Lowest legal TCP/UDP port. 0 is the "ephemeral" sentinel and is never a
    /// match server target.</summary>
    public const int MinPort = 1;

    /// <summary>Highest legal TCP/UDP port — the 16-bit max.</summary>
    public const int MaxPort = 65535;

    /// <summary>Loopback hostname used by the default. Kept as a constant so the
    /// resolver + settings screen agree on the canonical "local" target.</summary>
    public const string LoopbackHost = "127.0.0.1";

    /// <summary>Default UDP port for the Garupan match server. Mirrors
    /// <c>NetworkMatchEndpoint.DefaultPort</c> + <c>ServerConsoleOptionsParser.DefaultPort</c>.</summary>
    public const int DefaultPort = 7777;

    public string Host { get; init; } = LoopbackHost;

    public int Port { get; init; } = DefaultPort;

    /// <summary>Optional override the resolver picks when a Hungry Battles (free-for-all)
    /// match is dialed. <see cref="MultiplayerEndpointOverride.None"/> means "use the
    /// default Host/Port above". Local test dev hosts one Hungry server per port; this
    /// field binds that server to the matching lobby card without per-tester reconfig.</summary>
    public MultiplayerEndpointOverride HungryBattles { get; init; } = MultiplayerEndpointOverride.None;

    /// <summary>Optional override the resolver picks when a Tactical 5v5 (team) match is
    /// dialed. Same shape as <see cref="HungryBattles"/>; local test tactical server
    /// can live on a different host:port without breaking the FFA path.</summary>
    public MultiplayerEndpointOverride Tactical { get; init; } = MultiplayerEndpointOverride.None;

    public static MultiplayerSettings Default => new();
}
