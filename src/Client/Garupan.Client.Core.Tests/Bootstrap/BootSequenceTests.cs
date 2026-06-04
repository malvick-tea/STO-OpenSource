using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Garupan.Client.Core.Bootstrap;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Garupan.Client.Core.Tests.Bootstrap;

public sealed class BootSequenceTests
{
    [Fact]
    public async Task Runs_stages_in_order_ascending()
    {
        var executed = new List<string>();
        var stages = new IBootStage[]
        {
            new RecordingStage("third", 30, executed),
            new RecordingStage("first", 10, executed),
            new RecordingStage("second", 20, executed),
        };

        var seq = new BootSequence(stages, NullLogger<BootSequence>.Instance);
        await seq.RunAsync(NullContext(), CancellationToken.None);

        executed.Should().Equal("first", "second", "third");
    }

    [Fact]
    public async Task Wraps_stage_failure_in_BootFailureException()
    {
        var boom = new ThrowingStage("boom", 10, new InvalidOperationException("nope"));
        var seq = new BootSequence(new IBootStage[] { boom }, NullLogger<BootSequence>.Instance);

        Func<Task> act = () => seq.RunAsync(NullContext(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BootFailureException>();
        ex.Which.Stage.Should().BeSameAs(boom);
        ex.Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task Cancellation_propagates_OperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var seq = new BootSequence(
            new IBootStage[] { new RecordingStage("never", 10, new List<string>()) },
            NullLogger<BootSequence>.Instance);

        Func<Task> act = () => seq.RunAsync(NullContext(), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Fires_StageStarted_and_StageCompleted_per_stage()
    {
        var started = new List<string>();
        var completed = new List<string>();
        var seq = new BootSequence(
            new IBootStage[] { new RecordingStage("a", 10, new List<string>()) },
            NullLogger<BootSequence>.Instance);
        seq.StageStarted += s => started.Add(s.Name);
        seq.StageCompleted += (s, _) => completed.Add(s.Name);

        await seq.RunAsync(NullContext(), CancellationToken.None);

        started.Should().Equal("a");
        completed.Should().Equal("a");
    }

    private static BootContext NullContext() =>
        new(
            services: NoopServiceProvider.Instance,
            window: new NoopWindowService(),
            vfs: new NoopVfs(),
            lifecycle: new NoopLifecycle(),
            mainThread: new NoopDispatcher(),
            logger: NullLogger.Instance);

    private sealed class RecordingStage : IBootStage
    {
        private readonly List<string> _sink;

        public RecordingStage(string name, int order, List<string> sink)
        {
            Name = name;
            Order = order;
            _sink = sink;
        }

        public string Name { get; }

        public int Order { get; }

        public Task ExecuteAsync(BootContext ctx, CancellationToken ct)
        {
            _sink.Add(Name);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingStage : IBootStage
    {
        private readonly Exception _ex;

        public ThrowingStage(string name, int order, Exception ex)
        {
            Name = name;
            Order = order;
            _ex = ex;
        }

        public string Name { get; }

        public int Order { get; }

        public Task ExecuteAsync(BootContext ctx, CancellationToken ct) => Task.FromException(_ex);
    }
}
