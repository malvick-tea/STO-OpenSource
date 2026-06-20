using Garupan.Sim.Components;
using Garupan.Sim.Protocol;
using Microsoft.Extensions.Logging;
using Opus.Net.Transport;

namespace Garupan.Server.Match;

public sealed partial class MatchHost
{
    /// <summary>Disconnects one seated player by authoritative network id.</summary>
    public bool TryKickPlayer(uint networkId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_roster.TryGetByNetworkId(networkId, out var player))
        {
            return false;
        }

        _logger.LogWarning(
            "Kicking peer {Peer}: network_id={NetworkId}.",
            player.Connection,
            networkId);
        _transport.Disconnect(player.Connection);
        return true;
    }

    private void OnPeerDisconnected(ConnectionId peer)
    {
        if (!_roster.Remove(peer, out var player))
        {
            return;
        }

        _inputGuard.Remove(peer);
        if (_world.IsAlive(player.Entity))
        {
            _world.Destroy(player.Entity);
        }

        _logger.LogInformation(
            "Peer {Peer} left match: network_id={NetworkId}, remaining_players={Count}.",
            peer,
            player.NetworkId,
            _roster.Count);
    }

    private void OnClientInputReceived(ConnectionId peer, ClientInputFrame frame)
    {
        if (!_roster.TryGet(peer, out var player))
        {
            _logger.LogWarning("Dropped input from unknown peer {Peer}.", peer);
            return;
        }

        if (!_world.IsAlive(player.Entity))
        {
            _logger.LogWarning(
                "Dropped input from {Peer}: entity {Entity} is no longer alive.",
                peer,
                player.Entity);
            return;
        }

        if (!_inputGuard.Accept(peer, player.NetworkId, _time.Tick, frame))
        {
            _logger.LogWarning(
                "Dropped invalid or replayed input from {Peer}: client_tick={ClientTick}, server_tick={ServerTick}, network_id={NetworkId}.",
                peer,
                frame.Tick,
                _time.Tick.Value,
                frame.NetworkId);
            return;
        }

        _world.AddOrSet(player.Entity, new PendingInput
        {
            Tick = frame.Tick,
            Throttle = frame.Throttle,
            Steering = frame.Steering,
            TurretYawRadians = frame.TurretYawRadians,
            BarrelPitchRadians = frame.BarrelPitchRadians,
            Flags = frame.Flags,
        });
    }
}
