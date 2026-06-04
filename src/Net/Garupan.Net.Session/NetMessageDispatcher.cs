using System;
using Garupan.Sim.Protocol;
using Garupan.Sim.Snapshot;

namespace Garupan.Net.Session;

/// <summary>Pure-CPU classifier: given a received datagram, decides which Sim codec
/// should parse it. Reads the 4-byte ASCII magic prefix every Sim wire format declares
/// (<c>"SVOW"</c> / <c>"SVOI"</c> / <c>"SVOS"</c>) and returns the matching
/// <see cref="NetMessageKind"/>. Buffers shorter than the prefix or with an unrecognised
/// prefix return <see cref="NetMessageKind.Unknown"/> — the consumer logs + drops.
/// <para>
/// Stateless + allocation-free: a <see cref="ReadOnlySpan{T}"/> peek with three
/// SequenceEqual comparisons. Sessions consult this on every received event before
/// invoking the decoder, so the cost matters in the per-tick budget on a many-peer
/// server.
/// </para></summary>
public static class NetMessageDispatcher
{
    /// <summary>Length of the magic-prefix field every Sim wire format begins with.</summary>
    public const int MagicPrefixBytes = 4;

    public static NetMessageKind Classify(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < MagicPrefixBytes)
        {
            return NetMessageKind.Unknown;
        }

        var magic = payload[..MagicPrefixBytes];

        if (magic.SequenceEqual(WelcomeWire.Magic))
        {
            return NetMessageKind.Welcome;
        }

        if (magic.SequenceEqual(ClientInputWire.Magic))
        {
            return NetMessageKind.ClientInput;
        }

        if (magic.SequenceEqual(SnapshotWire.Magic))
        {
            return NetMessageKind.Snapshot;
        }

        if (magic.SequenceEqual(MatchOverWire.Magic))
        {
            return NetMessageKind.MatchOver;
        }

        return NetMessageKind.Unknown;
    }
}
