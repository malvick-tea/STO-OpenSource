using System.Collections.Generic;
using Garupan.Sim.Protocol;
using Opus.Foundation;
using Opus.Net.Transport;

namespace Garupan.Server.Match;

internal sealed class ClientInputGuard
{
    private const ulong MaxPastTicks = 120;
    private const ulong MaxFutureTicks = 8;
    private const int MaxInputsPerServerTick = 4;

    private readonly Dictionary<ConnectionId, PeerInputState> _states = new();

    public bool Accept(
        ConnectionId peer,
        uint expectedNetworkId,
        Tick serverTick,
        ClientInputFrame frame)
    {
        if (frame.NetworkId != expectedNetworkId || !ClientInputValidation.IsValid(frame))
        {
            return false;
        }

        var current = serverTick.Value < 0 ? 0UL : (ulong)serverTick.Value;
        if ((frame.Tick > current && frame.Tick - current > MaxFutureTicks)
            || (current > frame.Tick && current - frame.Tick > MaxPastTicks))
        {
            return false;
        }

        if (!_states.TryGetValue(peer, out var state))
        {
            state = new PeerInputState();
            _states.Add(peer, state);
        }

        if (state.HasAcceptedInput && frame.Tick <= state.LastClientTick)
        {
            return false;
        }

        if (state.ServerTick != current)
        {
            state.ServerTick = current;
            state.AcceptedThisServerTick = 0;
        }

        if (state.AcceptedThisServerTick >= MaxInputsPerServerTick)
        {
            return false;
        }

        state.HasAcceptedInput = true;
        state.LastClientTick = frame.Tick;
        state.AcceptedThisServerTick++;
        return true;
    }

    public void Remove(ConnectionId peer) => _states.Remove(peer);

    public void Clear() => _states.Clear();

    private sealed class PeerInputState
    {
        public bool HasAcceptedInput { get; set; }

        public ulong LastClientTick { get; set; }

        public ulong ServerTick { get; set; } = ulong.MaxValue;

        public int AcceptedThisServerTick { get; set; }
    }
}
