using System.Collections.Generic;
using Garupan.Sim.Components;
using Opus.Net.Transport;

namespace Garupan.Server.Match;

/// <summary>The set of peers seated in one <see cref="MatchHost"/> match, keyed by their
/// transport <see cref="ConnectionId"/>, together with the monotonic identity allocators
/// every new seat draws from.
/// <para>
/// Both the spawn-slot index and the <see cref="NetworkId"/> come from never-decremented
/// counters: a <see cref="NetworkId"/> is never reused, so a disconnect + reconnect — or a
/// whole-match recycle through <see cref="MatchHost.ResetMatch"/> — cannot alias a fresh
/// peer onto a departed peer's identity. <see cref="Clear"/> empties the seating but
/// deliberately leaves the counters intact, preserving that guarantee across a recycle.
/// </para>
/// <para>
/// Extracted from <see cref="MatchHost"/> so the host stays orchestration-only under the
/// senior-quality line cap; the never-reuse invariant is then unit-testable on its own.
/// </para></summary>
internal sealed class MatchRoster
{
    private readonly Dictionary<ConnectionId, ConnectedPlayer> _players = new();

    // Starts at 1 so id 0 stays free as the "not a networked entity" sentinel, matching
    // the codebase's None == 0 convention. Never decremented.
    private uint _nextNetworkId = 1;
    private int _nextSpawnIndex;

    /// <summary>How many peers are currently seated.</summary>
    public int Count => _players.Count;

    /// <summary>The seated peers — walked to rebuild the outcome roster each tick and to
    /// re-spawn every peer on a match recycle.</summary>
    public IReadOnlyCollection<ConnectedPlayer> Players => _players.Values;

    /// <summary>Draws the next spawn-slot index: a monotonic counter that positions the
    /// Nth peer ever to join along the host's spawn line.</summary>
    public int DrawSpawnIndex() => _nextSpawnIndex++;

    /// <summary>Draws the next never-reused <see cref="NetworkId"/>.</summary>
    public NetworkId DrawNetworkId() => new(_nextNetworkId++);

    /// <summary>Seats <paramref name="player"/>, keyed by its
    /// <see cref="ConnectedPlayer.Connection"/>. Re-seating the same connection — the
    /// match-recycle path — overwrites the prior row.</summary>
    public void Seat(ConnectedPlayer player) => _players[player.Connection] = player;

    /// <summary>Looks up the peer seated for <paramref name="connection"/>.</summary>
    public bool TryGet(ConnectionId connection, out ConnectedPlayer player) =>
        _players.TryGetValue(connection, out player);

    /// <summary>Removes the peer seated for <paramref name="connection"/>, handing it back
    /// in <paramref name="player"/>. False when the connection was not seated.</summary>
    public bool Remove(ConnectionId connection, out ConnectedPlayer player) =>
        _players.Remove(connection, out player);

    /// <summary>Empties the seating. The identity counters are left untouched so a later
    /// <see cref="DrawNetworkId"/> still cannot collide with a past id.</summary>
    public void Clear() => _players.Clear();
}
