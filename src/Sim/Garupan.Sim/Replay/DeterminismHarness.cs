using System;
using System.Security.Cryptography;
using Garupan.Sim.Snapshot;
using Garupan.Sim.Systems;
using Opus.Foundation;

namespace Garupan.Sim.Replay;

/// <summary>
/// Reusable scaffold for determinism tests: takes a scenario builder, runs N fixed
/// ticks with the canonical pipeline, captures snapshots, and returns the SHA-256 hex
/// digest of the resulting replay byte stream. Two callsites with the same arguments
/// must produce the same digest — a mismatch is a non-deterministic regression in the
/// sim (FP intrinsic drift, archetype-order instability, uninitialised state, etc.).
/// </summary>
/// <remarks>
/// Scenarios live in <see cref="DeterminismScenarios"/>; the replay-matrix tests pin a
/// SHA-256 per (scenario, tick-count) tuple. The harness intentionally exposes both a
/// hash-only API (<see cref="ComputeReplayHash"/>) and the raw byte stream
/// (<see cref="RunAndRecord"/>) so tests can diff trace files when a hash mismatch
/// surfaces.
/// </remarks>
public static class DeterminismHarness
{
    /// <summary>Runs <see cref="DeterminismScenarios.BuildSinglePair"/> for
    /// <paramref name="tickCount"/> ticks and returns the SHA-256 hex digest. Kept for
    /// backward compat with the M3l-era determinism tests.</summary>
    public static string ComputeReplayHash(
        int tickCount,
        ReadOnlySpan<byte> authenticationKey) =>
        ComputeReplayHash(
            DeterminismScenarios.BuildSinglePair,
            tickCount,
            authenticationKey);

    /// <summary>Builds a world via <paramref name="scenarioBuilder"/>, runs
    /// <paramref name="tickCount"/> ticks through the canonical pipeline, and returns
    /// the SHA-256 hex digest of the recorded replay bytes.</summary>
    public static string ComputeReplayHash(
        Action<World> scenarioBuilder,
        int tickCount,
        ReadOnlySpan<byte> authenticationKey)
    {
        ArgumentNullException.ThrowIfNull(scenarioBuilder);
        var bytes = RunAndRecord(scenarioBuilder, tickCount, authenticationKey);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    /// <summary>Runs a scenario and returns the raw replay byte stream — diagnostic
    /// alternative to <see cref="ComputeReplayHash"/>. Tests can diff streams between
    /// runs to localise where a determinism regression introduces a difference.</summary>
    public static byte[] RunAndRecord(
        Action<World> scenarioBuilder,
        int tickCount,
        ReadOnlySpan<byte> authenticationKey)
    {
        ArgumentNullException.ThrowIfNull(scenarioBuilder);
        if (tickCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickCount), tickCount, "tickCount must be positive");
        }

        using var world = World.Create();
        scenarioBuilder(world);

        var pipeline = BuildCanonicalPipeline();
        var time = GameTime.AtRate(SimulationConstants.TicksPerSecond);
        var writer = new ReplayWriter(SimulationConstants.TicksPerSecond, time.Tick);

        for (var i = 0; i < tickCount; i++)
        {
            pipeline.Tick(world, time, SimSeed.Zero);
            time = time.Advance();
            writer.RecordSnapshot(SnapshotCapture.Capture(world, time.Tick));
        }

        return writer.Build(authenticationKey);
    }

    private static SystemPipeline BuildCanonicalPipeline() =>
        new(new ISystem[]
        {
            new ApplyInputsSystem(),
            new AiBotSystem(),
            new HullDriveSystem(),
            new TurretAimSystem(),
            new ReloadTickSystem(),
            new ProjectileIntegrateSystem(),
            new GunFireSystem(),
            new ProjectileHitResolveSystem(),
            new LifetimeDecaySystem(),
            new CleanupDeadSystem(),
        });
}
