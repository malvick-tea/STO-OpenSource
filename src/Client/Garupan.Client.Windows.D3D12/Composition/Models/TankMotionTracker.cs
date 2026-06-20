using System;
using System.Collections.Generic;
using System.Numerics;
using Garupan.Client.Ui.Match.Network;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>Reconstructs visual track travel from authoritative inter-snapshot pose deltas.
/// The simulation deliberately does not replicate velocity, so this state lives only in the
/// renderer and advances at most once per snapshot tick.</summary>
internal sealed class TankMotionTracker
{
    private const float HalfTrackGaugeMeters = 1.35f;
    private readonly Dictionary<int, TankMotionState> _states = new();

    public void Reset() => _states.Clear();

    public TankMotion Resolve(in TankPlacement tank, long snapshotTick)
    {
        if (!_states.TryGetValue(tank.EntityId, out var state) || snapshotTick < state.SnapshotTick)
        {
            state = new TankMotionState(snapshotTick, tank.Position, tank.HullYawRadians, 0f, 0f);
            _states[tank.EntityId] = state;
            return new TankMotion(state.LeftTravelMeters, state.RightTravelMeters);
        }

        if (snapshotTick == state.SnapshotTick)
        {
            return new TankMotion(state.LeftTravelMeters, state.RightTravelMeters);
        }

        var delta = tank.Position - state.Position;
        var heading = new Vector2(MathF.Cos(tank.HullYawRadians), MathF.Sin(tank.HullYawRadians));
        var forwardTravel = Vector2.Dot(new Vector2(delta.X, delta.Z), heading);
        var yawDelta = WrapSignedPi(tank.HullYawRadians - state.HullYawRadians);
        var leftTravel = state.LeftTravelMeters + forwardTravel - (yawDelta * HalfTrackGaugeMeters);
        var rightTravel = state.RightTravelMeters + forwardTravel + (yawDelta * HalfTrackGaugeMeters);

        _states[tank.EntityId] = new TankMotionState(
            snapshotTick,
            tank.Position,
            tank.HullYawRadians,
            leftTravel,
            rightTravel);
        return new TankMotion(leftTravel, rightTravel);
    }

    private static float WrapSignedPi(float angle)
    {
        var shifted = angle + MathF.PI;
        var folded = shifted - (MathF.Floor(shifted / (2f * MathF.PI)) * (2f * MathF.PI));
        return folded - MathF.PI;
    }

    private readonly record struct TankMotionState(
        long SnapshotTick,
        Vector3 Position,
        float HullYawRadians,
        float LeftTravelMeters,
        float RightTravelMeters);
}

internal readonly record struct TankMotion(float LeftTravelMeters, float RightTravelMeters);
