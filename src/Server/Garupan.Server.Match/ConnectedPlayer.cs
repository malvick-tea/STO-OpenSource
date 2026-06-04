using Garupan.Sim;
using Garupan.Sim.Components;
using Opus.Net.Transport;

namespace Garupan.Server.Match;

/// <summary>One server-side row of "a peer is in this match". Tracks the transport-level
/// handle (used to send back to the peer), the Sim-level entity handle (used to route
/// incoming <c>ClientInputFrame</c>s into the right tank), and the team the peer was
/// balanced onto at join time.
/// <para>
/// <see cref="NetworkId"/> is the value sent in the peer's <c>WelcomeFrame</c> so the
/// client can pick its own row out of every subsequent <c>WorldSnapshot</c>. The host
/// draws it from a monotonic, never-reused counter and stamps the matching
/// <see cref="Garupan.Sim.Components.NetworkId"/> component onto the peer's tank, so
/// <see cref="Garupan.Sim.Snapshot.SnapshotCapture"/> emits the very same id on the wire.
/// </para>
/// <para>
/// <see cref="Team"/> mirrors the <c>TeamTag</c> stamped on the tank — a team is never
/// reassigned after spawn, so the roster row is an authoritative copy the outcome
/// evaluation reads without a per-tick ECS query.
/// </para></summary>
internal readonly record struct ConnectedPlayer(
    ConnectionId Connection,
    uint NetworkId,
    EntityHandle Entity,
    int SpawnIndex,
    Team Team);
