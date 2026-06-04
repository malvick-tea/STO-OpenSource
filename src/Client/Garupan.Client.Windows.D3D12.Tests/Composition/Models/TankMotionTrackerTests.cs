using System.Numerics;
using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Garupan.Client.Windows.Direct3D12.Composition.Models;
using Xunit;

namespace Garupan.Client.Windows.Direct3D12.Tests.Composition.Models;

public sealed class TankMotionTrackerTests
{
    [Fact]
    public void Forward_motion_advances_both_tracks_once_per_snapshot()
    {
        var tracker = new TankMotionTracker();
        tracker.Resolve(TankAt(0f), snapshotTick: 1);

        var first = tracker.Resolve(TankAt(2f), snapshotTick: 2);
        var repeated = tracker.Resolve(TankAt(2f), snapshotTick: 2);

        first.LeftTravelMeters.Should().BeApproximately(2f, 1e-5f);
        first.RightTravelMeters.Should().BeApproximately(2f, 1e-5f);
        repeated.Should().Be(first);
    }

    [Fact]
    public void Hull_rotation_advances_tracks_in_opposite_directions()
    {
        var tracker = new TankMotionTracker();
        tracker.Resolve(TankAt(0f), snapshotTick: 1);

        var motion = tracker.Resolve(TankAt(0f, yaw: 0.5f), snapshotTick: 2);

        motion.LeftTravelMeters.Should().BeLessThan(0f);
        motion.RightTravelMeters.Should().BeGreaterThan(0f);
    }

    private static TankPlacement TankAt(float x, float yaw = 0f) =>
        new(new Vector3(x, 0f, 0f), yaw, IsSelf: true, KnockedOut: false, EntityId: 7);
}
