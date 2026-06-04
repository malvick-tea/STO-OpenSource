using System;
using Arch.Core;
using Garupan.Sim.Components;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Match;

/// <summary>
/// Draws the world layer of the match viewport: field background, 10-metre grid, tanks
/// (hull-circle + heading line + turret barrel), and projectiles. Pure read-from-ECS —
/// never mutates the world.
///
/// Separated from <see cref="Screens.Match.MatchScreen"/> so that the screen handles
/// orchestration and the renderer handles drawing; the seam at <see cref="MatchViewport"/>
/// keeps both sides agnostic of the coordinate-system arithmetic.
/// </summary>
public sealed class MatchWorldRenderer
{
    private const float GridStepMeters = 10f;
    private const float TankRadiusMeters = 2.5f;

    public void Render(IDrawSurface surface, MatchSession session, MatchViewport viewport)
    {
        DrawField(surface, viewport);
        DrawGrid(surface, viewport);
        DrawBorder(surface, viewport);
        DrawTanks(surface, session, viewport);
        DrawProjectiles(surface, session, viewport);
    }

    private static void DrawField(IDrawSurface surface, MatchViewport viewport) =>
        surface.FillRect(viewport.X, viewport.Y, viewport.Width, viewport.Height, MatchPalette.FieldBg);

    private static void DrawBorder(IDrawSurface surface, MatchViewport viewport) =>
        surface.StrokeRect(viewport.X, viewport.Y, viewport.Width, viewport.Height, 2, MatchPalette.FieldBorder);

    private static void DrawGrid(IDrawSurface surface, MatchViewport viewport)
    {
        for (var v = -viewport.HalfExtentMeters; v <= viewport.HalfExtentMeters; v += GridStepMeters)
        {
            var (sx, _) = viewport.WorldToScreen(new System.Numerics.Vector2(v, 0f));
            surface.DrawLine(sx, viewport.Y, sx, viewport.Y + viewport.Height, 1, MatchPalette.FieldGrid);
            var (_, sy) = viewport.WorldToScreen(new System.Numerics.Vector2(0f, v));
            surface.DrawLine(viewport.X, sy, viewport.X + viewport.Width, sy, 1, MatchPalette.FieldGrid);
        }
    }

    private static void DrawTanks(IDrawSurface surface, MatchSession session, MatchViewport viewport)
    {
        var raw = session.World.Raw;
        var query = new QueryDescription().WithAll<Transform, TeamTag, Hull>();
        raw.Query(in query, (Entity e, ref Transform tf, ref TeamTag team, ref Hull _) =>
        {
            var (sx, sy) = viewport.WorldToScreen(tf.Position);
            var radius = (int)MathF.Max(8f, TankRadiusMeters * viewport.PixelsPerMeter);

            var color = raw.Has<KnockedOut>(e)
                ? MatchPalette.KnockedOut
                : team.Team == Team.PlayerSchool
                    ? MatchPalette.PlayerTeam
                    : MatchPalette.OpponentTeam;

            surface.FillCircle(sx, sy, radius, color);
            surface.StrokeCircle(sx, sy, radius, 2, MatchPalette.Outline);

            DrawHullHeading(surface, sx, sy, radius, tf.YawRadians);
            if (raw.Has<Turret>(e))
            {
                ref var turret = ref raw.Get<Turret>(e);
                DrawTurretBarrel(surface, sx, sy, radius, turret.YawRadians);
            }
        });
    }

    private static void DrawHullHeading(IDrawSurface surface, int sx, int sy, int radius, float yaw)
    {
        var dx = (int)(MathF.Cos(yaw) * (radius + 6));
        var dy = (int)(MathF.Sin(yaw) * (radius + 6));
        // -dy because screen Y grows down.
        surface.DrawLine(sx, sy, sx + dx, sy - dy, 2, MatchPalette.Heading);
    }

    private static void DrawTurretBarrel(IDrawSurface surface, int sx, int sy, int radius, float yaw)
    {
        var bx = (int)(MathF.Cos(yaw) * (radius + 12));
        var by = (int)(MathF.Sin(yaw) * (radius + 12));
        surface.DrawLine(sx, sy, sx + bx, sy - by, 3, MatchPalette.Foreground);
    }

    private static void DrawProjectiles(IDrawSurface surface, MatchSession session, MatchViewport viewport)
    {
        var raw = session.World.Raw;
        var query = new QueryDescription().WithAll<Transform, Projectile>();
        raw.Query(in query, (ref Transform tf, ref Projectile _) =>
        {
            var (sx, sy) = viewport.WorldToScreen(tf.Position);
            surface.FillCircle(sx, sy, 3, MatchPalette.Bullet);
        });
    }
}
