using System;
using System.Collections.Generic;
using System.Numerics;
using Garupan.Content;
using Garupan.Sim.Snapshot;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Foundation;

namespace Garupan.Garage.Demo;

/// <summary>
/// Pure functions that bridge Sim-side top-down XY state to the renderer's 3D XZ world.
/// All methods are deterministic and side-effect-free — they take primitive Sim outputs
/// (positions, yaws, ticks, snapshots) and return the matrices the
/// <see cref="GarageSceneController"/> consumes.
/// </summary>
/// <remarks>
/// Coord convention: Sim is top-down XY (Y is north). 3D world is right-handed Y-up,
/// so Sim <c>+Y</c> maps to 3D <c>-Z</c> (forward-into-screen). Sim yaw (CCW positive
/// around the up axis) is negated to keep visual rotation consistent in the right-handed
/// 3D view. Y in 3D stays at zero for tanks — Phase-0 sim has no elevation.
///
/// All animation timing is keyed off Sim ticks, not wall-clock seconds, so two parallel
/// runs of the same match (with the same <c>SimSeed</c>) reach identical animation states
/// at identical ticks — see <c>docs/adr/0027-tick-based-animation.md</c>.
/// </remarks>
internal static class SimToWorldMapper
{
    /// <summary>Pitch angle (around the world +X axis) at the end of the KO-tilt animation.
    /// 25° forward reads as a hull settling after a track-snap without driving the model
    /// entirely through the floor plane.</summary>
    public const float KnockedOutPitchRadians = MathF.PI / 7.2f;

    /// <summary>Wall-clock duration of the KO-tilt animation (linear ease).</summary>
    public const float KnockedOutAnimationSeconds = 0.5f;

    /// <summary>Inverse of the Sim tick rate. Derived from
    /// <see cref="GameTime.DefaultTickRateHz"/> so a future rate change in Foundation
    /// propagates here without code edits — the demo's <see cref="SimTankDriver"/>
    /// constructs a default-rate <c>FixedStepLoop</c> and Sim doesn't change tick rates
    /// at runtime.</summary>
    public const float SimTickSeconds = 1f / GameTime.DefaultTickRateHz;

    /// <summary>Default projectile height for hand-built snapshots. Fired rounds carry
    /// their actual pitched muzzle height in <see cref="ProjectileSnapshot"/>.</summary>
    public const float MuzzleHeightMeters = 1.942544f;

    /// <summary>Number of trail samples per in-flight projectile: 1 head cube + 3 echo
    /// cubes stepped back along the velocity vector at <see cref="ProjectileTrailEchoSpacingSeconds"/>
    /// intervals.</summary>
    public const int ProjectileTrailEchoes = 4;

    /// <summary>Wall-clock spacing between adjacent projectile-trail echo positions. Four
    /// echoes × 0.05 s = 0.15 s of visible trail length.</summary>
    public const float ProjectileTrailEchoSpacingSeconds = 0.05f;

    public static Vector3 SimXyToWorldXz(Vector2 simXy) => new(simXy.X, 0f, -simXy.Y);

    /// <summary>Fraction (0..1) of the KO-tilt animation that should be applied at the
    /// current tick. Returns 0 if <paramref name="koTick"/> is null (still alive) or if
    /// <paramref name="currentTick"/> is somehow earlier than <paramref name="koTick"/>.
    /// Reaches 1 at <see cref="KnockedOutAnimationSeconds"/> after the KO tick.</summary>
    public static float KnockedOutProgress(long? koTick, long currentTick)
    {
        if (koTick is null)
        {
            return 0f;
        }

        var ticksSince = currentTick - koTick.Value;
        if (ticksSince <= 0)
        {
            return 0f;
        }

        var seconds = ticksSince * SimTickSeconds;
        return Math.Clamp(seconds / KnockedOutAnimationSeconds, 0f, 1f);
    }

    /// <summary>Builds the world matrix for a tank pose, premultiplying the KO-tilt
    /// rotation when <paramref name="koTick"/> is set. Sim XY → 3D XZ remap + yaw
    /// negation live here; callers pass primitive Sim outputs.</summary>
    public static Matrix4x4 BuildTankWorld(Vector2 simPosition, float simYawRadians, long? koTick, long currentTick)
    {
        var pos = SimXyToWorldXz(simPosition);
        var yaw = -simYawRadians;
        var world = GarageSceneController.BuildTankWorld(pos, yaw);
        var progress = KnockedOutProgress(koTick, currentTick);
        if (progress > 0f)
        {
            world = Matrix4x4.CreateRotationX(KnockedOutPitchRadians * progress) * world;
        }

        return world;
    }

    /// <summary>Builds the projectile trail matrices for one snapshot. Output sized
    /// <c>projectiles.Count × <see cref="ProjectileTrailEchoes"/></c>; each round emits
    /// a head cube at its current position plus N-1 echo cubes stepped back along the
    /// negated velocity vector. Returns an empty array when no rounds are in flight to
    /// avoid per-frame allocation cost.</summary>
    public static Matrix4x4[] BuildProjectileTrail(IReadOnlyList<ProjectileSnapshot> projectiles)
    {
        if (projectiles.Count == 0)
        {
            return Array.Empty<Matrix4x4>();
        }

        var worlds = new Matrix4x4[projectiles.Count * ProjectileTrailEchoes];
        var write = 0;
        for (var i = 0; i < projectiles.Count; i++)
        {
            var p = projectiles[i];
            var velocityXz = new Vector3(p.Velocity.X, p.VerticalVelocityMps, -p.Velocity.Y);
            var headXz = SimXyToWorldXz(p.Position);
            for (var echo = 0; echo < ProjectileTrailEchoes; echo++)
            {
                var pos = headXz - (velocityXz * (echo * ProjectileTrailEchoSpacingSeconds));
                pos.Y += p.VisualHeightMeters;
                worlds[write++] = Matrix4x4.CreateTranslation(pos);
            }
        }

        return worlds;
    }

    /// <summary>Builds one shell-head world matrix per projectile whose
    /// <see cref="ProjectileSnapshot.Family"/> has a binding in <paramref name="catalog"/>.
    /// Position lifts to the projectile's captured muzzle height; orientation aligns the
    /// shell's local +X nose axis with the full world velocity. Projectiles whose family
    /// is absent from the catalog (or whose velocity is below
    /// <see cref="MinShellSpeedSquaredForOrientation"/>) fall through to a plain
    /// translation, so the renderer still places a shell head at the right spot for a
    /// just-spawned round.</summary>
    public static Matrix4x4[] BuildShellHeads(
        IReadOnlyList<ProjectileSnapshot> projectiles,
        ShellVisualCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(projectiles);
        ArgumentNullException.ThrowIfNull(catalog);
        if (projectiles.Count == 0)
        {
            return Array.Empty<Matrix4x4>();
        }

        var matchCount = CountMatchedProjectiles(projectiles, catalog);
        if (matchCount == 0)
        {
            return Array.Empty<Matrix4x4>();
        }

        var heads = new Matrix4x4[matchCount];
        var write = 0;
        for (var i = 0; i < projectiles.Count; i++)
        {
            var p = projectiles[i];
            if (!catalog.Contains(ToContentAmmoType(p.Family)))
            {
                continue;
            }

            var pos = SimXyToWorldXz(p.Position);
            pos.Y = p.VisualHeightMeters;
            var velXz = new Vector3(p.Velocity.X, p.VerticalVelocityMps, -p.Velocity.Y);
            heads[write++] = BuildShellWorld(pos, velXz);
        }

        return heads;
    }

    /// <summary>The Sim and Content layers ship mirrored <c>AmmoType</c> enums whose
    /// numeric values are pinned to match (see <c>Opus.Content/AmmoType.cs</c> remarks).
    /// This cast is the cross-layer adapter the demo uses to query the Content-tier shell
    /// catalog with a Sim-tier <see cref="ProjectileSnapshot.Family"/>.</summary>
    private static AmmoType ToContentAmmoType(Sim.Components.AmmoType simAmmo) =>
        (AmmoType)(byte)simAmmo;

    /// <summary>Below this squared-speed threshold a shell head skips velocity-aligned
    /// orientation and falls back to a physically-scaled translation. Guards the
    /// velocity normalization from a degenerate vector and keeps a
    /// just-spawned round visible (the cube echo trail starts a frame later).</summary>
    public const float MinShellSpeedSquaredForOrientation = 1e-6f;

    private static int CountMatchedProjectiles(
        IReadOnlyList<ProjectileSnapshot> projectiles,
        ShellVisualCatalog catalog)
    {
        var count = 0;
        for (var i = 0; i < projectiles.Count; i++)
        {
            if (catalog.Contains(ToContentAmmoType(projectiles[i].Family)))
            {
                count++;
            }
        }

        return count;
    }

    private static Matrix4x4 BuildShellWorld(Vector3 position, Vector3 velocityXz)
    {
        if (velocityXz.LengthSquared() < MinShellSpeedSquaredForOrientation)
        {
            return Matrix4x4.CreateScale(ShellVisualGeometry.PzGr39DisplayScale)
                * Matrix4x4.CreateTranslation(position);
        }

        var heading = MathF.Atan2(-velocityXz.Z, velocityXz.X);
        var horizontalSpeed = MathF.Sqrt((velocityXz.X * velocityXz.X) + (velocityXz.Z * velocityXz.Z));
        var pitch = MathF.Atan2(velocityXz.Y, horizontalSpeed);
        return Matrix4x4.CreateScale(ShellVisualGeometry.PzGr39DisplayScale)
            * Matrix4x4.CreateFromAxisAngle(Vector3.UnitZ, pitch)
            * Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, heading)
            * Matrix4x4.CreateTranslation(position);
    }
}
