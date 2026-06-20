using FluentAssertions;
using Garupan.Sim.Replay;
using Xunit;

namespace Garupan.Sim.Tests.Replay;

/// <summary>
/// Replay-matrix golden-hash gate. Each scenario's SHA-256 is pinned below — a mismatch
/// means the deterministic property of the sim has shifted, either because a system
/// introduced wall-clock dependency / non-deterministic iteration order / floating-point
/// intrinsic drift, OR because a sim change is *intentionally* different. In the latter
/// case run the harness once and paste the new digest below; in the former, find the
/// regression.
/// </summary>
/// <remarks>
/// Hashes are uppercase hex. Pinned tick counts are short enough to keep the tests fast
/// (&lt; 50 ms each on this machine) while long enough to exercise the
/// projectile / hit / knockout / cleanup chain. The matrix doubles as the canonical
/// regression net before the alpha-bound milestones layer new sim systems.
/// </remarks>
public sealed class ReplayMatrixTests
{
    // Re-pinned on 2026-06-03 (snapshot wire v7→v8): the codec now appends a felled-prop section to
    // every frame so destructible street clutter breaks authoritatively on the client. The harness
    // hashes the encoded snapshot bytes, so the new 4-byte prop-count prefix shifts all three digests
    // even though these prop-free scenarios always encode an empty section. The sim itself is
    // unchanged (the repeat-hash test is still green, and ALL three scenarios moved by exactly the
    // wire prefix — no behaviour drift). Prior 2026-06-03 re-pin was for the medium tank player tank
    // + per-tank rolling resistance + body-height silhouette.
    private const string SinglePair60TickHash =
        "466A069076675FB157CDC8465DAC6E369DDE4A51CD123D7E4CC6B5FB33CCDC98";

    private const string MultiOpponent90TickHash =
        "9853B16E1074F606769B20A7C774646D6D8358D2147CEB17DB4E8B27A8A76AC3";

    private const string PointBlank120TickHash =
        "D3D1F6F6ABC0FC01D24D93D9E3B1903F46C26A7D80F8B0066464A26E720CFA2E";

    [Fact]
    public void Single_pair_scenario_at_60_ticks_matches_pinned_hash()
    {
        var hash = DeterminismHarness.ComputeReplayHash(
            DeterminismScenarios.BuildSinglePair,
            60,
            ReplayTestKeys.IntegrityKey);
        hash.Should().Be(
            SinglePair60TickHash,
            "single-pair determinism digest moved — see ReplayMatrixTests.cs for guidance");
    }

    [Fact]
    public void Multi_opponent_scenario_at_90_ticks_matches_pinned_hash()
    {
        var hash = DeterminismHarness.ComputeReplayHash(
            DeterminismScenarios.BuildMultiOpponent,
            90,
            ReplayTestKeys.IntegrityKey);
        hash.Should().Be(
            MultiOpponent90TickHash,
            "multi-opponent determinism digest moved — see ReplayMatrixTests.cs for guidance");
    }

    [Fact]
    public void Point_blank_scenario_at_120_ticks_matches_pinned_hash()
    {
        var hash = DeterminismHarness.ComputeReplayHash(
            DeterminismScenarios.BuildPointBlankExchange,
            120,
            ReplayTestKeys.IntegrityKey);
        hash.Should().Be(
            PointBlank120TickHash,
            "point-blank determinism digest moved — see ReplayMatrixTests.cs for guidance");
    }

    [Fact]
    public void Re_running_each_scenario_yields_identical_hashes()
    {
        // Cross-check: even with the pinned constants, verify in-process repeat
        // determinism. Catches any harness-side state that leaks between runs (e.g.
        // a static cache silently polluting the second pass).
        var single = DeterminismHarness.ComputeReplayHash(
            DeterminismScenarios.BuildSinglePair,
            30,
            ReplayTestKeys.IntegrityKey);
        var singleAgain = DeterminismHarness.ComputeReplayHash(
            DeterminismScenarios.BuildSinglePair,
            30,
            ReplayTestKeys.IntegrityKey);
        single.Should().Be(singleAgain);

        var multi = DeterminismHarness.ComputeReplayHash(
            DeterminismScenarios.BuildMultiOpponent,
            45,
            ReplayTestKeys.IntegrityKey);
        var multiAgain = DeterminismHarness.ComputeReplayHash(
            DeterminismScenarios.BuildMultiOpponent,
            45,
            ReplayTestKeys.IntegrityKey);
        multi.Should().Be(multiAgain);
    }
}
