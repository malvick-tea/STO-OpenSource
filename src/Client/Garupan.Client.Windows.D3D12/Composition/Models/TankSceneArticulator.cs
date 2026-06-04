using System;
using System.Collections.Generic;
using System.Numerics;
using Garupan.Client.Ui.Match.Network;
using Opus.Content.Meshes;
using Opus.Engine.Renderer.Direct3D12.Assets;

namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>Builds one posed draw list from the named nodes emitted by the medium tank OBJ rig
/// exporter. Opus remains model-agnostic; tank-specific node roles stay in the game client.</summary>
internal sealed class TankSceneArticulator
{
    private const string TurretYawNodeName = "turret_yaw";
    private const string BarrelPitchNodeName = "barrel_pitch";
    private const string LeftWheelPrefix = "wheel_spin_l_";
    private const string RightWheelPrefix = "wheel_spin_r_";
    private const float WheelRadiusMeters = 0.35f;
    private const float TrackUvRepeatMeters = 1.25f;
    private static readonly Vector4 TrackTint = new(0.42f, 0.42f, 0.42f, 1f);

    private readonly GltfScene _scene;
    private readonly IReadOnlyList<SceneNodeDraw> _template;
    private readonly Matrix4x4[] _authoredLocals;
    private readonly int _turretYawNodeIndex;
    private readonly int _barrelPitchNodeIndex;
    private readonly int[] _leftWheelNodeIndices;
    private readonly int[] _rightWheelNodeIndices;

    public TankSceneArticulator(GltfScene scene, IReadOnlyList<SceneNodeDraw> template)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(template);

        _scene = scene;
        _template = template;
        _authoredLocals = new Matrix4x4[scene.Nodes.Length];
        var leftWheels = new List<int>();
        var rightWheels = new List<int>();
        _turretYawNodeIndex = -1;
        _barrelPitchNodeIndex = -1;

        for (var i = 0; i < scene.Nodes.Length; i++)
        {
            var node = scene.Nodes[i];
            _authoredLocals[i] = node.LocalTransform;
            if (string.Equals(node.Name, TurretYawNodeName, StringComparison.Ordinal))
            {
                _turretYawNodeIndex = i;
            }
            else if (string.Equals(node.Name, BarrelPitchNodeName, StringComparison.Ordinal))
            {
                _barrelPitchNodeIndex = i;
            }
            else if (node.Name.StartsWith(LeftWheelPrefix, StringComparison.Ordinal))
            {
                leftWheels.Add(i);
            }
            else if (node.Name.StartsWith(RightWheelPrefix, StringComparison.Ordinal))
            {
                rightWheels.Add(i);
            }
        }

        if (_turretYawNodeIndex < 0)
        {
            throw new InvalidOperationException($"Tank scene is missing the '{TurretYawNodeName}' rig node.");
        }

        if (_barrelPitchNodeIndex < 0)
        {
            throw new InvalidOperationException($"Tank scene is missing the '{BarrelPitchNodeName}' rig node.");
        }

        if (leftWheels.Count == 0 || rightWheels.Count == 0)
        {
            throw new InvalidOperationException("Tank scene is missing exported wheel_spin_l_* or wheel_spin_r_* rig nodes.");
        }

        _leftWheelNodeIndices = leftWheels.ToArray();
        _rightWheelNodeIndices = rightWheels.ToArray();
    }

    public List<SceneNodeDraw> BuildDraws(
        in TankPlacement tank,
        in Matrix4x4 tankWorld,
        in Vector4 tint,
        in TankMotion motion)
    {
        var locals = (Matrix4x4[])_authoredLocals.Clone();
        var turretLocalYaw = -(tank.TurretYawRadians - tank.HullYawRadians);
        locals[_turretYawNodeIndex] =
            Matrix4x4.CreateRotationY(turretLocalYaw) * _authoredLocals[_turretYawNodeIndex];
        locals[_barrelPitchNodeIndex] =
            BarrelPose(tank.BarrelPitchRadians, tank.GunRecoilTravelMeters) * _authoredLocals[_barrelPitchNodeIndex];

        ApplyWheelSpin(locals, _leftWheelNodeIndices, motion.LeftTravelMeters);
        ApplyWheelSpin(locals, _rightWheelNodeIndices, motion.RightTravelMeters);

        var worlds = SceneTreeMath.ComputeWorldTransforms(_scene, locals);
        var result = new List<SceneNodeDraw>(_template.Count);
        for (var i = 0; i < _template.Count; i++)
        {
            var draw = _template[i];
            var posedWorld = draw.NodeIndex >= 0 && draw.NodeIndex < worlds.Length
                ? worlds[draw.NodeIndex]
                : draw.World;
            var uvOffset = draw.UvOffset + TrackUvOffset(draw.NodeIndex, in motion);
            var partTint = IsTrackNode(draw.NodeIndex) ? TrackTint : Vector4.One;
            result.Add(draw with
            {
                World = posedWorld * tankWorld,
                TintFactor = draw.TintFactor * tint * partTint,
                UvOffset = uvOffset,
            });
        }

        return result;
    }

    /// <summary>Elevation rotation around the <c>barrel_pitch</c> pivot authored into the
    /// rigged GLB. The exported pivot is already seated at the barrel base. Adding a
    /// runtime translation here makes the whole gun slide vertically as it pitches.</summary>
    internal static Matrix4x4 BarrelPitchRotation(float barrelPitchRadians) =>
        Matrix4x4.CreateRotationX(-barrelPitchRadians);

    /// <summary>Gun assembly pose at one replicated tick: the recoiling group first travels
    /// backward along its bore, then inherits the ordinary elevation pivot.</summary>
    internal static Matrix4x4 BarrelPose(float barrelPitchRadians, float recoilTravelMeters) =>
        Matrix4x4.CreateTranslation(0f, 0f, -MathF.Max(0f, recoilTravelMeters))
        * BarrelPitchRotation(barrelPitchRadians);

    private void ApplyWheelSpin(Matrix4x4[] locals, IReadOnlyList<int> nodeIndices, float travelMeters)
    {
        var spin = Matrix4x4.CreateRotationX(-travelMeters / WheelRadiusMeters);
        for (var i = 0; i < nodeIndices.Count; i++)
        {
            var nodeIndex = nodeIndices[i];
            locals[nodeIndex] = spin * _authoredLocals[nodeIndex];
        }
    }

    private Vector2 TrackUvOffset(int nodeIndex, in TankMotion motion)
    {
        if (nodeIndex < 0 || nodeIndex >= _scene.Nodes.Length)
        {
            return Vector2.Zero;
        }

        var nodeName = _scene.Nodes[nodeIndex].Name;
        if (nodeName.Contains("#track_l", StringComparison.Ordinal))
        {
            return new Vector2(0f, WrappedUv(motion.LeftTravelMeters));
        }

        if (nodeName.Contains("#track_r", StringComparison.Ordinal))
        {
            return new Vector2(0f, WrappedUv(motion.RightTravelMeters));
        }

        return Vector2.Zero;
    }

    private bool IsTrackNode(int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= _scene.Nodes.Length)
        {
            return false;
        }

        var nodeName = _scene.Nodes[nodeIndex].Name;
        return nodeName.Contains("#track_l", StringComparison.Ordinal) ||
               nodeName.Contains("#track_r", StringComparison.Ordinal);
    }

    private static float WrappedUv(float travelMeters)
    {
        var repeats = travelMeters / TrackUvRepeatMeters;
        return repeats - MathF.Floor(repeats);
    }
}
