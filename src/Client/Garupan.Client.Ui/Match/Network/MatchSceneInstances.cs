using System;
using System.Collections.Generic;
using System.Numerics;
using Garupan.Content;
using Garupan.Sim.Terrain;

namespace Garupan.Client.Ui.Match.Network;

/// <summary>One tank ready to submit to the 3D scene renderer: its world transform, the
/// RGBA tint factor (0..1) multiplied onto the model's albedo, and whether it is the local
/// player (the renderer maps that onto the scene builder's player vs opponent slots).</summary>
public readonly record struct MatchTankInstance(Matrix4x4 World, Vector4 Tint, bool IsSelf);

/// <summary>
/// Pure translation of a <see cref="NetworkMatchScenePlan"/>'s tank placements into world
/// matrices + tints for the 3D scene renderer. No GPU — just the transform + tint maths —
/// so it is unit-testable headless and the D3D12 match renderer stays a thin shell over it.
/// </summary>
public static class MatchSceneInstances
{
    /// <summary>Uniform model scale. The bundled tank glTF is authored in metres, so a
    /// world-space placement needs no rescale; named for the GPU calibration pass.</summary>
    public const float TankScale = 1f;

    /// <summary>Degrees added to the world hull yaw to align the model's authored forward
    /// with the +X (east) convention the projection uses. Calibrated on the GPU pass; 0
    /// means the model already faces +X at zero yaw. The OBJ-derived the medium tank faces local
    /// +Z after Blender's glTF coordinate conversion, so +90 degrees aligns it to +X.</summary>
    public const float ModelForwardYawOffsetDegrees = 90f;

    /// <summary>Display-only scale applied to the normalized one-metre PzGr 39 shell model.
    /// It is deliberately exaggerated for chase-camera readability; ballistics and hit
    /// resolution retain their physical catalogue values. The model is authored
    /// nose-along-+X lying flat.</summary>
    public const float ShellDisplayScale = ShellVisualGeometry.PzGr39DisplayScale;

    private const float DegreesToRadians = System.MathF.PI / 180f;

    /// <summary>Below this squared speed a round is treated as stationary (nose-along-+X),
    /// so a just-spawned near-zero-velocity round doesn't snap to a noisy heading.</summary>
    private const float ShellHeadingEpsilonSq = 1e-4f;

    /// <summary>Half the length / width of a representative tank's track footprint — the span a rigid
    /// hull bridges. Seating samples the surface at these offsets and fits a plane, so a sub-footprint
    /// feature (a felled pole under one corner) lifts that corner into a gentle roll instead of
    /// tipping the whole deck to a point normal. A nominal ~6 m x 2.9 m hull suffices for visual
    /// seating; exact per-chassis dimensions are not plumbed for a placeholder-box battlefield.</summary>
    private const float HullFootprintHalfLengthMeters = 3f;
    private const float HullFootprintHalfWidthMeters = 1.45f;

    /// <summary>White: a live tank renders with its own textures, untinted (Pillar-2 — no
    /// team colour, no markers).</summary>
    private static readonly Vector4 LiveTint = Vector4.One;

    /// <summary>A knocked-out tank is darkened to read as a wreck.</summary>
    private static readonly Vector4 KnockedOutTint = new(0.27f, 0.27f, 0.31f, 1f);

    public static IReadOnlyList<MatchTankInstance> From(NetworkMatchScenePlan plan) => From(plan, terrain: null);

    /// <summary>As <see cref="From(NetworkMatchScenePlan)"/>, but conforms every hull to the
    /// terrain when a height field is supplied: each tank is seated at the sampled surface height
    /// and tilted so its deck lies along the slope normal, instead of floating level at Y=0. One
    /// code path for all tanks, so the catalogue's 100+ chassis need no per-model work.</summary>
    public static IReadOnlyList<MatchTankInstance> From(NetworkMatchScenePlan plan, IHeightSurface? terrain)
    {
        Opus.Foundation.Ensure.NotNull(plan);
        var instances = new MatchTankInstance[plan.Tanks.Count];
        for (var i = 0; i < plan.Tanks.Count; i++)
        {
            var tank = plan.Tanks[i];
            instances[i] = new MatchTankInstance(WorldOf(tank, terrain), TintOf(tank), tank.IsSelf);
        }

        return instances;
    }

    /// <summary>World transforms for the plan's in-flight shells — one per round, the
    /// nose-along-+X shell model uniformly scaled, yawed onto the round's horizontal
    /// velocity, and placed at its position. Empty when no rounds are in flight.</summary>
    public static IReadOnlyList<Matrix4x4> ProjectileWorlds(NetworkMatchScenePlan plan)
    {
        Opus.Foundation.Ensure.NotNull(plan);
        var worlds = new Matrix4x4[plan.Projectiles.Count];
        for (var i = 0; i < plan.Projectiles.Count; i++)
        {
            worlds[i] = ShellWorld(plan.Projectiles[i]);
        }

        return worlds;
    }

    /// <summary>A round's world transform: the PzGr 39 shell (authored nose-along-+X, lying
    /// flat) uniformly scaled, then yawed about +Y so its nose points along the round's
    /// horizontal velocity — a real shell flies point-first and lying down, never standing
    /// up. A near-stationary round keeps its nose on +X rather than snapping to noise.</summary>
    private static Matrix4x4 ShellWorld(ProjectilePlacement projectile)
    {
        var velocity = projectile.Velocity;
        var heading = velocity.LengthSquared() > ShellHeadingEpsilonSq
            ? System.MathF.Atan2(-velocity.Z, velocity.X)
            : 0f;
        var horizontalSpeed = System.MathF.Sqrt((velocity.X * velocity.X) + (velocity.Z * velocity.Z));
        var pitch = velocity.LengthSquared() > ShellHeadingEpsilonSq
            ? System.MathF.Atan2(velocity.Y, horizontalSpeed)
            : 0f;
        return Matrix4x4.CreateScale(ShellDisplayScale)
            * Matrix4x4.CreateFromAxisAngle(Vector3.UnitZ, pitch)
            * Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, heading)
            * Matrix4x4.CreateTranslation(projectile.Position);
    }

    private static Matrix4x4 WorldOf(TankPlacement tank, IHeightSurface? terrain)
    {
        // Sim yaw grows from +X toward +Y. After mapping sim +Y to render +Z, System.Numerics'
        // Y-axis rotation needs the opposite sign to keep D/right steering visually right.
        var yaw = -tank.HullYawRadians + (ModelForwardYawOffsetDegrees * DegreesToRadians);
        var heading = Matrix4x4.CreateScale(TankScale) * Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, yaw);
        if (terrain is null)
        {
            return heading * Matrix4x4.CreateTranslation(tank.Position);
        }

        var x = tank.Position.X;
        var z = tank.Position.Z;
        var (height, normal) = FootprintSurfaceFit.At(
            terrain, x, z, tank.HullYawRadians, HullFootprintHalfLengthMeters, HullFootprintHalfWidthMeters);
        var tilt = TiltToNormal(normal);
        return heading * tilt * Matrix4x4.CreateTranslation(x, height, z);
    }

    /// <summary>Rotation that tips the model's up-axis (+Y) onto the terrain normal, about the
    /// horizontal axis between them, so a hull on a slope pitches/rolls to lie on the surface
    /// rather than floating level. Identity on flat ground (normal == +Y). Applied after the yaw
    /// so the heading still reads correctly, now inclined along the slope.</summary>
    private static Matrix4x4 TiltToNormal(Vector3 normal)
    {
        var axis = Vector3.Cross(Vector3.UnitY, normal);
        var axisLength = axis.Length();
        if (axisLength < 1e-6f)
        {
            return Matrix4x4.Identity;
        }

        var angle = MathF.Acos(Math.Clamp(normal.Y, -1f, 1f));
        return Matrix4x4.CreateFromAxisAngle(axis / axisLength, angle);
    }

    private static Vector4 TintOf(TankPlacement tank) => tank.KnockedOut ? KnockedOutTint : LiveTint;
}
