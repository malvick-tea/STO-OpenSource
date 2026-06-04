using System;
using System.Collections.Generic;
using Garupan.Sim.Protocol;
using Garupan.Sim.Snapshot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Opus.Net.Transport;

namespace Garupan.Net.Session;

/// <summary>Server-side session router. Composes a single <see cref="INetTransport"/>
/// (the loopback transport is 1:1; a future hub transport will surface multiple peers
/// through the same Pump loop). On <see cref="Pump"/>: drains transport events and
/// dispatches; outgoing helpers encode through the matching Sim codec and send to the
/// chosen peer.
/// <para>
/// Peers come and go: the router maintains the live set so <see cref="BroadcastSnapshot"/>
/// can fan out to every connected client with one call. Each peer's lifetime is
/// (Connected → … → Disconnected) — neither event ordering nor reentrancy is reordered
/// by the session; whatever order the transport surfaces, the session preserves.
/// </para>
/// <para>
/// Outgoing-buffer scratch is sized for the largest fixed-size server → client frame
/// (Welcome or match-over). Snapshot frames are variable-length so
/// <see cref="BroadcastSnapshot"/> rents a per-call buffer sized via
/// <see cref="SnapshotWire.EncodedSize"/> — game-tick frequency is low enough (≤ 60 Hz)
/// that a per-broadcast allocation is acceptable for now; if the snapshot channel
/// becomes a hot spot, swap to a pooled buffer.
/// </para></summary>
public sealed class ServerSession : IDisposable
{
    private static readonly int OutboundFixedScratchBytes =
        Math.Max(WelcomeWire.FrameBytes, MatchOverWire.FrameBytes);

    private readonly INetTransport _transport;
    private readonly ILogger<ServerSession> _logger;
    private readonly List<NetEvent> _eventScratch = new();
    private readonly HashSet<ConnectionId> _peers = new();
    private readonly byte[] _outboundFixedScratch = new byte[OutboundFixedScratchBytes];
    private bool _disposed;

    public ServerSession(INetTransport transport, ILogger<ServerSession>? logger = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _logger = logger ?? NullLogger<ServerSession>.Instance;
    }

    /// <summary>Snapshot of the connected peer set. Stable inside one <see cref="Pump"/>
    /// call. Test-friendly + readable for diagnostics; the hot path uses the internal
    /// collection directly.</summary>
    public IReadOnlyCollection<ConnectionId> ConnectedPeers => _peers;

    /// <summary>Raised when a transport surfaces a peer connect. Hosts can immediately
    /// <see cref="SendWelcome"/> in response — the peer is registered in
    /// <see cref="ConnectedPeers"/> before this fires.</summary>
    public event Action<ConnectionId>? PeerConnected;

    /// <summary>Raised when a transport surfaces a peer disconnect. The peer has been
    /// removed from <see cref="ConnectedPeers"/> before this fires.</summary>
    public event Action<ConnectionId>? PeerDisconnected;

    /// <summary>Raised when a peer sent a <see cref="ClientInputFrame"/>. Server game
    /// logic applies the inputs into the authoritative Sim world.</summary>
    public event Action<ConnectionId, ClientInputFrame>? ClientInputReceived;

    /// <summary>Drains one tick's worth of transport events and dispatches each.
    /// Idempotent on an empty queue.</summary>
    public void Pump()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _transport.Poll(_eventScratch);
        foreach (var evt in _eventScratch)
        {
            Dispatch(evt);
        }
    }

    /// <summary>Encodes <paramref name="frame"/> through <see cref="WelcomeCodec"/> and
    /// sends to <paramref name="peer"/>. Returns false when the peer is unknown, the
    /// transport rejected the send, or the session is disposed.</summary>
    public bool SendWelcome(ConnectionId peer, WelcomeFrame frame)
    {
        if (_disposed || !_peers.Contains(peer))
        {
            return false;
        }

        var written = WelcomeCodec.Encode(frame, _outboundFixedScratch);
        return _transport.Send(peer, _outboundFixedScratch.AsSpan(0, written));
    }

    /// <summary>Encodes <paramref name="snap"/> through <see cref="SnapshotEncoder"/> once
    /// and sends to every connected peer. Returns the number of peers the transport
    /// accepted the send for — equal to <see cref="ConnectedPeers"/> count when every
    /// link is healthy.</summary>
    public int BroadcastSnapshot(WorldSnapshot snap)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var buffer = new byte[SnapshotWire.EncodedSize(snap)];
        var written = SnapshotEncoder.Encode(snap, buffer);
        var slice = buffer.AsSpan(0, written);

        var delivered = 0;
        foreach (var peer in _peers)
        {
            if (_transport.Send(peer, slice))
            {
                delivered++;
            }
        }

        return delivered;
    }

    /// <summary>Encodes <paramref name="frame"/> through <see cref="MatchOverCodec"/> and
    /// sends the match verdict to every connected peer. Returns the number of peers the
    /// transport accepted the send for. A one-shot broadcast — the host calls it on the
    /// single tick the match is decided.</summary>
    public int BroadcastMatchOver(MatchOverFrame frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var written = MatchOverCodec.Encode(frame, _outboundFixedScratch);
        var slice = _outboundFixedScratch.AsSpan(0, written);

        var delivered = 0;
        foreach (var peer in _peers)
        {
            if (_transport.Send(peer, slice))
            {
                delivered++;
            }
        }

        return delivered;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PeerConnected = null;
        PeerDisconnected = null;
        ClientInputReceived = null;
    }

    private void Dispatch(NetEvent evt)
    {
        switch (evt.Kind)
        {
            case NetEventKind.Connected:
                if (_peers.Add(evt.Connection))
                {
                    PeerConnected?.Invoke(evt.Connection);
                }

                break;
            case NetEventKind.Disconnected:
                if (_peers.Remove(evt.Connection))
                {
                    PeerDisconnected?.Invoke(evt.Connection);
                }

                break;
            case NetEventKind.Received:
                DispatchReceived(evt.Connection, evt.Payload);
                break;
        }
    }

    private void DispatchReceived(ConnectionId peer, byte[] payload)
    {
        var kind = NetMessageDispatcher.Classify(payload);
        switch (kind)
        {
            case NetMessageKind.ClientInput:
                if (ClientInputCodec.TryDecode(payload, out var input).Ok)
                {
                    ClientInputReceived?.Invoke(peer, input);
                }
                else
                {
                    _logger.LogWarning("Discarded client-input payload from {Peer}: decode failed.", peer);
                }

                break;
            case NetMessageKind.Welcome:
            case NetMessageKind.Snapshot:
            case NetMessageKind.MatchOver:
                _logger.LogWarning("Server received a {Kind} payload from {Peer} — wrong-direction frame.", kind, peer);
                break;
            default:
                _logger.LogWarning("Server received unknown payload from {Peer} ({Bytes} bytes).", peer, payload.Length);
                break;
        }
    }
}
