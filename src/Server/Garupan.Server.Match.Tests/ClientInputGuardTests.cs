using Garupan.Sim.Components;
using Garupan.Sim.Protocol;
using Opus.Foundation;
using Opus.Net.Transport;
using Xunit;

namespace Garupan.Server.Match.Tests;

public sealed class ClientInputGuardTests
{
    private static readonly ConnectionId Peer = new(7UL);

    [Fact]
    public void Accepts_valid_monotonic_input()
    {
        var guard = new ClientInputGuard();

        Assert.True(guard.Accept(Peer, 11u, new Tick(100), Frame(100, 11u)));
        Assert.True(guard.Accept(Peer, 11u, new Tick(100), Frame(101, 11u)));
    }

    [Fact]
    public void Rejects_wrong_network_identity()
    {
        var guard = new ClientInputGuard();

        Assert.False(guard.Accept(Peer, 11u, new Tick(100), Frame(100, 12u)));
    }

    [Fact]
    public void Rejects_replayed_or_out_of_order_tick()
    {
        var guard = new ClientInputGuard();
        Assert.True(guard.Accept(Peer, 11u, new Tick(100), Frame(100, 11u)));

        Assert.False(guard.Accept(Peer, 11u, new Tick(100), Frame(100, 11u)));
        Assert.False(guard.Accept(Peer, 11u, new Tick(100), Frame(99, 11u)));
    }

    [Fact]
    public void Rejects_ticks_outside_server_window()
    {
        var guard = new ClientInputGuard();

        Assert.False(guard.Accept(Peer, 11u, new Tick(200), Frame(79, 11u)));
        Assert.False(guard.Accept(Peer, 11u, new Tick(200), Frame(209, 11u)));
    }

    [Fact]
    public void Limits_inputs_per_server_tick()
    {
        var guard = new ClientInputGuard();
        for (ulong tick = 100; tick < 104; tick++)
        {
            Assert.True(guard.Accept(Peer, 11u, new Tick(100), Frame(tick, 11u)));
        }

        Assert.False(guard.Accept(Peer, 11u, new Tick(100), Frame(104, 11u)));
        Assert.True(guard.Accept(Peer, 11u, new Tick(101), Frame(105, 11u)));
    }

    [Fact]
    public void Rejects_non_finite_or_out_of_range_values()
    {
        var guard = new ClientInputGuard();

        Assert.False(guard.Accept(
            Peer,
            11u,
            new Tick(100),
            Frame(100, 11u) with { Throttle = float.NaN }));
        Assert.False(guard.Accept(
            Peer,
            11u,
            new Tick(100),
            Frame(100, 11u) with { Steering = 1.01f }));
        Assert.False(guard.Accept(
            Peer,
            11u,
            new Tick(100),
            Frame(100, 11u) with { Flags = (InputFlags)2u }));
    }

    private static ClientInputFrame Frame(ulong tick, uint networkId) => new(
        Tick: tick,
        NetworkId: networkId,
        Throttle: 0.5f,
        Steering: -0.25f,
        TurretYawRadians: 0.75f,
        Flags: InputFlags.Fire,
        BarrelPitchRadians: -0.1f);
}
