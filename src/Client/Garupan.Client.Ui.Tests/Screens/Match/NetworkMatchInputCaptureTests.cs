using System;
using System.Numerics;
using FluentAssertions;
using Garupan.Client.Core.Application;
using Garupan.Client.Ui.Screens.Match;
using Garupan.Client.Ui.Tests.Fixtures;
using Garupan.Sim.Components;
using Garupan.Sim.Protocol;
using Garupan.Sim.Snapshot;
using Opus.Engine.Input;
using Opus.Foundation;
using Xunit;

namespace Garupan.Client.Ui.Tests.Screens.Match;

/// <summary>Headless coverage for the network input-frame composer and local-row lookup.</summary>
public sealed class NetworkMatchInputCaptureTests
{
    private static readonly InputBindings Defaults = InputBindings.Default;

    [Fact]
    public void FindSelf_returns_null_for_a_null_snapshot()
    {
        NetworkMatchInputCapture.FindSelf(null, localNetworkId: 5u).Should().BeNull();
    }

    [Fact]
    public void FindSelf_returns_null_when_the_local_id_is_unassigned()
    {
        var snapshot = SnapshotWith(EntityAt(id: 0, x: 0f, y: 0f));

        NetworkMatchInputCapture.FindSelf(snapshot, localNetworkId: 0u).Should().BeNull();
    }

    [Fact]
    public void FindSelf_returns_the_row_whose_id_matches_the_local_network_id()
    {
        var mine = EntityAt(id: 7, x: 3f, y: 4f);
        var snapshot = SnapshotWith(EntityAt(id: 6, x: 0f, y: 0f), mine);

        NetworkMatchInputCapture.FindSelf(snapshot, localNetworkId: 7u).Should().Be(mine);
    }

    [Fact]
    public void FindSelf_returns_null_when_no_row_matches_the_local_id()
    {
        var snapshot = SnapshotWith(EntityAt(id: 6, x: 0f, y: 0f));

        NetworkMatchInputCapture.FindSelf(snapshot, localNetworkId: 7u).Should().BeNull();
    }

    [Fact]
    public void BuildInputFrame_stamps_the_supplied_tick_and_local_network_id()
    {
        var frame = Build(new FakeInputSource(), tick: 42, localNetworkId: 9u);

        frame.Tick.Should().Be(42u);
        frame.NetworkId.Should().Be(9u);
    }

    [Fact]
    public void BuildInputFrame_maps_a_neutral_keyboard_frame_to_zero_drive()
    {
        var frame = Build(new FakeInputSource());

        frame.Throttle.Should().Be(0f);
        frame.Steering.Should().Be(0f);
        frame.Flags.Should().Be(InputFlags.None);
    }

    [Fact]
    public void BuildInputFrame_maps_the_forward_key_to_full_throttle()
    {
        var frame = Build(new FakeInputSource().Hold(Key.W));

        frame.Throttle.Should().Be(1f);
    }

    [Fact]
    public void BuildInputFrame_sets_the_fire_flag_on_an_edge_triggered_fire()
    {
        var frame = Build(new FakeInputSource().Press(Key.Space));

        frame.Flags.Should().Be(InputFlags.Fire);
    }

    [Fact]
    public void BuildInputFrame_forwards_the_locally_maintained_turret_target()
    {
        var frame = Build(new FakeInputSource(), turretTargetYawRadians: 1.25f);

        frame.TurretYawRadians.Should().Be(1.25f);
    }

    [Fact]
    public void BuildInputFrame_forwards_the_locally_maintained_barrel_pitch()
    {
        var frame = Build(new FakeInputSource(), barrelPitchRadians: 0.2f);

        frame.BarrelPitchRadians.Should().Be(0.2f);
    }

    private static ClientInputFrame Build(
        IInputSource input,
        float turretTargetYawRadians = 0f,
        float barrelPitchRadians = 0f,
        ulong tick = 1,
        uint localNetworkId = 3u) =>
        NetworkMatchInputCapture.BuildInputFrame(
            tick,
            localNetworkId,
            input,
            Defaults,
            turretTargetYawRadians,
            barrelPitchRadians);

    private static EntitySnapshot EntityAt(int id, float x, float y) =>
        new(id, new Vector2(x, y), YawRadians: 0f, TurretYawRadians: 0f, StateFlags: EntityStateFlags.None);

    private static WorldSnapshot SnapshotWith(params EntitySnapshot[] entities) =>
        new(Tick.Zero, entities, Array.Empty<ProjectileSnapshot>());
}
