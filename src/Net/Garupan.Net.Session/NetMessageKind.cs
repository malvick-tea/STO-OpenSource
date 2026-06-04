namespace Garupan.Net.Session;

/// <summary>Wire-level classification of an incoming datagram, used by
/// <see cref="NetMessageDispatcher.Classify"/> + the session routers to pick which codec
/// decodes a payload. One member per magic prefix the Sim-tier codecs declare today:
/// <list type="bullet">
/// <item><description><see cref="Welcome"/> — "SVOW", server → client handshake (see
/// <see cref="Garupan.Sim.Protocol.WelcomeWire"/>).</description></item>
/// <item><description><see cref="ClientInput"/> — "SVOI", client → server per-tick input
/// frame (see <see cref="Garupan.Sim.Protocol.ClientInputWire"/>).</description></item>
/// <item><description><see cref="Snapshot"/> — "SVOS", server → client authoritative
/// world snapshot (see <see cref="Garupan.Sim.Snapshot.SnapshotWire"/>).</description></item>
/// <item><description><see cref="MatchOver"/> — "SVOO", server → client match-end
/// verdict (see <see cref="Garupan.Sim.Protocol.MatchOverWire"/>).</description></item>
/// <item><description><see cref="Unknown"/> — payload too short to classify or magic
/// doesn't match any known kind. Sessions log + drop.</description></item>
/// </list>
/// New wire types add a new member here + a new dispatch arm in the
/// <see cref="NetMessageDispatcher"/>. Once a session is built, the contract is "given a
/// payload, return its kind"; it doesn't decode or own buffer storage.</summary>
public enum NetMessageKind
{
    Unknown = 0,
    Welcome,
    ClientInput,
    Snapshot,
    MatchOver,
}
