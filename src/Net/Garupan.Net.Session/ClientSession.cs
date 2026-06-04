using System;
using System.Collections.Generic;
using Garupan.Sim.Protocol;
using Garupan.Sim.Snapshot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Opus.Net.Transport;

namespace Garupan.Net.Session;

/// <summary>Client-side session router. Owns no transport — composes one supplied by the
/// host (a single <see cref="INetTransport"/> talking to one server). On <see cref="Pump"/>:
/// drains every <see cref="NetEvent"/> the transport observed since the last pump and
/// dispatches to strongly-typed events. Outgoing <see cref="ClientInputFrame"/>s encode
/// through <see cref="ClientInputCodec"/> and ride the transport's <see cref="INetTransport.Send"/>.
/// <para>
/// Single-peer: <see cref="ServerPeer"/> is set on the first <see cref="NetEventKind.Connected"/>
/// event the transport surfaces and stays for the session's lifetime. A
/// <see cref="NetEventKind.Disconnected"/> resets it to <see cref="ConnectionId.None"/>
/// and raises <see cref="Disconnected"/>. Multi-server hosting is out of scope —
/// matchmaking is the layer that picks a server first.
/// </para>
/// <para>
/// Threading: built for the game-tick model — <see cref="Pump"/> runs on the caller's
/// thread, never on the transport's receive thread. The internal scratch list is reused
/// across pumps to keep the per-tick allocation profile flat. Outgoing-buffer scratch is
/// a small fixed array sized for the largest fixed-size frame the protocol declares.
/// </para></summary>
public sealed class ClientSession : IDisposable
{
    private static readonly int OutboundScratchBytes = ClientInputWire.FrameBytes;

    private readonly INetTransport _transport;
    private readonly ILogger<ClientSession> _logger;
    private readonly List<NetEvent> _eventScratch = new();
    private readonly byte[] _outboundScratch = new byte[OutboundScratchBytes];
    private ConnectionId _serverPeer = ConnectionId.None;
    private bool _disposed;

    public ClientSession(INetTransport transport, ILogger<ClientSession>? logger = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _logger = logger ?? NullLogger<ClientSession>.Instance;
    }

    /// <summary>The server's <see cref="ConnectionId"/> once the transport has surfaced the
    /// initial <see cref="NetEventKind.Connected"/>; <see cref="ConnectionId.None"/>
    /// before that and after a disconnect.</summary>
    public ConnectionId ServerPeer => _serverPeer;

    /// <summary>True between the first <see cref="Connected"/> and the next
    /// <see cref="Disconnected"/>. Game code checks before queuing input that depends on
    /// being in a session.</summary>
    public bool IsConnected => _serverPeer.IsValid;

    /// <summary>Raised once when the transport surfaces the initial Connected event.</summary>
    public event Action<ConnectionId>? Connected;

    /// <summary>Raised when the transport disconnects from the server side. After this
    /// fires <see cref="IsConnected"/> is false and <see cref="SendInput"/> returns
    /// false.</summary>
    public event Action<ConnectionId>? Disconnected;

    /// <summary>Raised when the transport surfaces a Welcome frame. The frame has already
    /// been decoded; consumers can read <see cref="WelcomeFrame.NetworkId"/> /
    /// <see cref="WelcomeFrame.TeamId"/> directly without touching raw bytes.</summary>
    public event Action<WelcomeFrame>? WelcomeReceived;

    /// <summary>Raised when the transport surfaces a Snapshot frame. The
    /// <see cref="WorldSnapshot"/> is fully decoded — game code applies it through the
    /// existing snapshot-apply pipeline.</summary>
    public event Action<WorldSnapshot>? SnapshotReceived;

    /// <summary>Raised when the transport surfaces a match-over frame — the server's
    /// one-shot verdict. After this the match is decided and the host has frozen the
    /// sim; no further snapshots arrive.</summary>
    public event Action<MatchOverFrame>? MatchOverReceived;

    /// <summary>Drains one tick's worth of transport events and dispatches each to the
    /// matching event. Idempotent on an empty queue; cheap to call every frame.</summary>
    public void Pump()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _transport.Poll(_eventScratch);
        foreach (var evt in _eventScratch)
        {
            Dispatch(evt);
        }
    }

    /// <summary>Encodes <paramref name="frame"/> through <see cref="ClientInputCodec"/>
    /// and sends to the server peer. Returns false when not connected, when the transport
    /// rejected the send (peer dropped between pumps), or when the session is disposed.
    /// </summary>
    public bool SendInput(ClientInputFrame frame)
    {
        if (_disposed || !_serverPeer.IsValid)
        {
            return false;
        }

        var written = ClientInputCodec.Encode(frame, _outboundScratch);
        return _transport.Send(_serverPeer, _outboundScratch.AsSpan(0, written));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Connected = null;
        Disconnected = null;
        WelcomeReceived = null;
        SnapshotReceived = null;
        MatchOverReceived = null;
    }

    private void Dispatch(NetEvent evt)
    {
        switch (evt.Kind)
        {
            case NetEventKind.Connected:
                _serverPeer = evt.Connection;
                Connected?.Invoke(evt.Connection);
                break;
            case NetEventKind.Disconnected:
                var droppedPeer = _serverPeer.IsValid ? _serverPeer : evt.Connection;
                _serverPeer = ConnectionId.None;
                Disconnected?.Invoke(droppedPeer);
                break;
            case NetEventKind.Received:
                DispatchReceived(evt.Payload);
                break;
        }
    }

    private void DispatchReceived(byte[] payload)
    {
        var kind = NetMessageDispatcher.Classify(payload);
        switch (kind)
        {
            case NetMessageKind.Welcome:
                if (WelcomeCodec.TryDecode(payload, out var welcome).Ok)
                {
                    WelcomeReceived?.Invoke(welcome);
                }
                else
                {
                    _logger.LogWarning("Discarded welcome payload: decode failed ({Bytes} bytes).", payload.Length);
                }

                break;
            case NetMessageKind.Snapshot:
                if (SnapshotDecoder.TryDecode(payload, out var snap).Ok)
                {
                    SnapshotReceived?.Invoke(snap);
                }
                else
                {
                    _logger.LogWarning("Discarded snapshot payload: decode failed ({Bytes} bytes).", payload.Length);
                }

                break;
            case NetMessageKind.MatchOver:
                if (MatchOverCodec.TryDecode(payload, out var matchOver).Ok)
                {
                    MatchOverReceived?.Invoke(matchOver);
                }
                else
                {
                    _logger.LogWarning("Discarded match-over payload: decode failed ({Bytes} bytes).", payload.Length);
                }

                break;
            case NetMessageKind.ClientInput:
                _logger.LogWarning("Client received a ClientInput payload — wrong-direction frame.");
                break;
            default:
                _logger.LogWarning("Client received unknown payload ({Bytes} bytes).", payload.Length);
                break;
        }
    }
}
