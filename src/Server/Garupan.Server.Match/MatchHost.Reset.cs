using System;
using System.Collections.Generic;
using Garupan.Sim.Spawn;
using Microsoft.Extensions.Logging;
using Opus.Net.Transport;

namespace Garupan.Server.Match;

public sealed partial class MatchHost
{
    public void ResetMatch()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var peersToRespawn = new List<ConnectionId>(_roster.Count);
        foreach (var player in _roster.Players)
        {
            peersToRespawn.Add(player.Connection);
            if (_world.IsAlive(player.Entity))
            {
                _world.Destroy(player.Entity);
            }
        }

        _roster.Clear();
        _outcomeTracker.Reset();
        _postMatchTicksRemaining = -1;
        _admissionPolicy.Reset();
        MapPropRoundReset.RestoreStanding(_world);

        foreach (var peer in peersToRespawn)
        {
            SpawnPlayerForPeer(peer);
        }

        _matchesPlayed++;
        _logger.LogInformation(
            "Match reset on tick {Tick}: next round = match #{Count}, {Players} players seated.",
            _time.Tick.Value,
            _matchesPlayed + 1,
            _roster.Count);
    }
}
