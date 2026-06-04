using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Garupan.Content;
using Garupan.Server.Console;
using Garupan.Server.Match;
using Garupan.Sim.Components;
using Opus.Net.Loopback;
using Xunit;

namespace Garupan.Server.Console.Tests;

[Collection(nameof(MatchHostTickLoopTests))]
[CollectionDefinition(nameof(MatchHostTickLoopTests), DisableParallelization = true)]
public sealed class MatchHostTickLoopTests
{
    [Fact]
    public void Loop_pumps_at_least_a_few_frames_within_a_short_window()
    {
        using var fixture = new LoopbackHostFixture();
        var loop = new MatchHostTickLoop(fixture.Host, framePumpHz: 240);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        loop.Run(cts.Token);

        loop.FramesPumped.Should().BeGreaterThan(10, "a 200 ms window at 240 Hz must comfortably exceed 10 pump iterations");
    }

    [Fact]
    public void Loop_returns_immediately_when_cancelled_before_start()
    {
        using var fixture = new LoopbackHostFixture();
        var loop = new MatchHostTickLoop(fixture.Host, framePumpHz: 120);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        loop.Run(cts.Token);

        loop.FramesPumped.Should().Be(0, "an already-cancelled token must exit before the first pump");
    }

    [Fact]
    public async Task Loop_stops_promptly_when_cancellation_lands_mid_run()
    {
        using var fixture = new LoopbackHostFixture();
        var loop = new MatchHostTickLoop(fixture.Host, framePumpHz: 240);
        using var cts = new CancellationTokenSource();

        var loopTask = Task.Run(() => loop.Run(cts.Token));
        await Task.Delay(50);
        cts.Cancel();

        var finished = await Task.WhenAny(loopTask, Task.Delay(TimeSpan.FromSeconds(2)));
        finished.Should().Be(loopTask, "loop must observe the cancellation token within the test budget");
        await loopTask;
    }

    [Fact]
    public void Loop_advances_the_underlying_match_host_tick()
    {
        using var fixture = new LoopbackHostFixture();
        var loop = new MatchHostTickLoop(fixture.Host, framePumpHz: 240);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        loop.Run(cts.Token);

        fixture.Host.CurrentTick.Value.Should().BeGreaterThan(0, "MatchHost's internal FixedStepLoop must advance through the Pump driver");
    }

    [Fact]
    public void Negative_pump_hz_is_rejected()
    {
        using var fixture = new LoopbackHostFixture();

        var act = () => new MatchHostTickLoop(fixture.Host, framePumpHz: -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private sealed class LoopbackHostFixture : IDisposable
    {
        private readonly LoopbackTransportLink _link;

        public LoopbackHostFixture()
        {
            _link = LoopbackTransportPair.Create();
            var options = new MatchHostOptions(
                PlayerSpec: TankRoster.VehicleMediumB,
                PlayerTeam: Team.PlayerSchool,
                SpawnAnchor: Vector2.Zero);
            Host = new MatchHost(_link.Server, options);
        }

        public MatchHost Host { get; }

        public void Dispose()
        {
            Host.Dispose();
            _link.Server.Dispose();
            _link.Client.Dispose();
        }
    }
}
