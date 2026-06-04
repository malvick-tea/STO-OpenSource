using Garupan.Client.Ui.Match;
using Garupan.Client.Ui.Match.Network;
using Garupan.Sim.Components;
using Garupan.Sim.Snapshot;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Match;

/// <summary>
/// Static dot-style renderer for the network match screen's first-cut visuals. Owns no
/// state — every method takes the surface + viewport + snapshot data it needs and emits
/// draw calls. Lives in its own type so <see cref="NetworkMatchScreen"/> stays focused
/// on lifecycle + input forwarding.
/// </summary>
internal static class NetworkMatchRenderer
{
    /// <summary>Vertical gap between the verdict headline and the "Esc to leave" sub-line.</summary>
    private const int VerdictHintGap = 16;

    public static void DrawGrid(IDrawSurface surface, MatchViewport viewport)
    {
        if (viewport.Size <= 0)
        {
            return;
        }

        surface.StrokeRect(viewport.X, viewport.Y, viewport.Size, viewport.Size, 1, NetworkMatchPalette.GridLine);
        var midX = viewport.X + (viewport.Size / 2);
        var midY = viewport.Y + (viewport.Size / 2);
        surface.DrawLine(viewport.X, midY, viewport.X + viewport.Size, midY, 1, NetworkMatchPalette.GridLine);
        surface.DrawLine(midX, viewport.Y, midX, viewport.Y + viewport.Size, 1, NetworkMatchPalette.GridLine);
    }

    public static void DrawTanks(
        IDrawSurface surface,
        MatchViewport viewport,
        WorldSnapshot snapshot,
        uint localNetworkId)
    {
        foreach (var entity in snapshot.Entities)
        {
            var (sx, sy) = viewport.WorldToScreen(entity.Position);
            var isSelf = (uint)entity.Id == localNetworkId && localNetworkId != 0;
            var knockedOut = (entity.StateFlags & EntityStateFlags.KnockedOut) != EntityStateFlags.None;
            var fill = ResolveTankColor(isSelf, knockedOut);
            surface.FillCircle(sx, sy, NetworkMatchPalette.TankRadiusPixels, fill);
            DrawTurret(surface, sx, sy, entity.TurretYawRadians, viewport.PixelsPerMeter);
        }
    }

    public static void DrawProjectiles(IDrawSurface surface, MatchViewport viewport, WorldSnapshot snapshot)
    {
        foreach (var projectile in snapshot.Projectiles)
        {
            var (sx, sy) = viewport.WorldToScreen(projectile.Position);
            surface.FillCircle(sx, sy, NetworkMatchPalette.ProjectileRadiusPixels, NetworkMatchPalette.Projectile);
        }
    }

    public static void DrawHint(IDrawSurface surface)
    {
        const string HintText = "WASD: drive  ·  Mouse: aim  ·  RMB: orbit  ·  Wheel: zoom  ·  Space: fire";
        var width = surface.MeasureText(HintText, NetworkMatchPalette.HintFontSize);
        surface.DrawText(
            HintText,
            (surface.Width - width) / 2,
            surface.Height - 28,
            NetworkMatchPalette.HintFontSize,
            NetworkMatchPalette.Dim);
    }

    /// <summary>Draws the mode-mismatch notice band directly below the top bar: the
    /// player picked one mode in the lobby but the server they reached is hosting
    /// another — a misconfigured Multiplayer-settings endpoint. Warn-coloured, one line;
    /// it informs without blocking play (the player is still in a real match).</summary>
    public static void DrawModeMismatch(IDrawSurface surface, string expectedLabel, string actualLabel)
    {
        surface.FillRect(
            0,
            NetworkMatchPalette.TopBarHeight,
            surface.Width,
            NetworkMatchPalette.MismatchBandHeight,
            NetworkMatchPalette.Panel);
        var text = $"MODE MISMATCH  ·  joined {actualLabel}, expected {expectedLabel}  ·  check Multiplayer settings";
        surface.DrawText(
            text,
            16,
            NetworkMatchPalette.TopBarHeight + NetworkMatchPalette.MismatchBandTextOffsetY,
            NetworkMatchPalette.HintFontSize,
            NetworkMatchPalette.Warn);
    }

    /// <summary>Draws the full-screen match-over banner: a translucent scrim, the
    /// VICTORY / DEFEAT / DRAW headline in the verdict colour, and an "Esc to leave"
    /// sub-line. Called last in the screen's render pass so it sits over the field.</summary>
    public static void DrawVerdict(IDrawSurface surface, NetworkMatchVerdict verdict)
    {
        surface.FillRect(0, 0, surface.Width, surface.Height, NetworkMatchPalette.VerdictScrim);

        var headline = ResolveVerdictText(verdict);
        var headlineWidth = surface.MeasureText(headline, NetworkMatchPalette.VerdictFontSize);
        var headlineY = (surface.Height / 2) - NetworkMatchPalette.VerdictFontSize;
        surface.DrawText(
            headline,
            (surface.Width - headlineWidth) / 2,
            headlineY,
            NetworkMatchPalette.VerdictFontSize,
            ResolveVerdictColor(verdict));

        const string LeaveHint = "Esc to leave";
        var hintWidth = surface.MeasureText(LeaveHint, NetworkMatchPalette.HintFontSize);
        surface.DrawText(
            LeaveHint,
            (surface.Width - hintWidth) / 2,
            headlineY + NetworkMatchPalette.VerdictFontSize + VerdictHintGap,
            NetworkMatchPalette.HintFontSize,
            NetworkMatchPalette.Dim);
    }

    private static void DrawTurret(IDrawSurface surface, int cx, int cy, float yawRadians, float pixelsPerMeter)
    {
        const float TurretMeters = 6f;
        var length = (int)(TurretMeters * pixelsPerMeter);
        if (length < 4)
        {
            length = 4;
        }

        var dx = (int)(System.MathF.Cos(yawRadians) * length);
        var dy = -(int)(System.MathF.Sin(yawRadians) * length);
        surface.DrawLine(cx, cy, cx + dx, cy + dy, 2, NetworkMatchPalette.Foreground);
    }

    private static Color ResolveTankColor(bool isSelf, bool knockedOut)
    {
        if (knockedOut)
        {
            return NetworkMatchPalette.KnockedOut;
        }

        return isSelf ? NetworkMatchPalette.SelfTank : NetworkMatchPalette.OtherTank;
    }

    private static string ResolveVerdictText(NetworkMatchVerdict verdict) => verdict switch
    {
        NetworkMatchVerdict.Victory => "VICTORY",
        NetworkMatchVerdict.Defeat => "DEFEAT",
        _ => "DRAW",
    };

    private static Color ResolveVerdictColor(NetworkMatchVerdict verdict) => verdict switch
    {
        NetworkMatchVerdict.Victory => NetworkMatchPalette.VerdictVictory,
        NetworkMatchVerdict.Defeat => NetworkMatchPalette.VerdictDefeat,
        _ => NetworkMatchPalette.VerdictDraw,
    };
}
