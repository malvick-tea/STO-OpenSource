using Garupan.Content;
using SimAmmoType = Garupan.Sim.Components.AmmoType;
using SimGun = Garupan.Sim.Components.Gun;

namespace Garupan.Client.Ui.Match;

/// <summary>
/// One frame's worth of HUD-relevant state, taken from a <see cref="MatchSession"/> at the
/// start of <see cref="Screens.Match.MatchScreen.Render"/>. Pure value type so the renderer
/// can be exercised without an ECS world — the <see cref="Capture"/> factory is the only
/// piece that touches the live session.
/// </summary>
/// <param name="AlivePlayers">Count of player-side tanks still alive.</param>
/// <param name="AliveOpponents">Count of opponent-side tanks still alive.</param>
/// <param name="IsPlayerAlive">False after the player tank is knocked out — the HUD hides
/// the ammo / reload chrome in that case rather than showing stale values.</param>
/// <param name="ReloadFraction">Reload progress in <c>[0, 1]</c>; 1 = ready to fire. See
/// <see cref="ReloadProgress.Of"/>.</param>
/// <param name="ChamberedRound">Family of the round currently in the breech. The HUD
/// displays its NATO acronym ("AP" / "APCR" / "HEAT" / "HE") — universal across locales.</param>
public readonly record struct MatchHudReadout(
    int AlivePlayers,
    int AliveOpponents,
    bool IsPlayerAlive,
    float ReloadFraction,
    AmmoType ChamberedRound)
{
    /// <summary>True when the reload bar should display "ready" (full + bright crimson).</summary>
    public bool IsReady => ReloadFraction >= 1f;

    /// <summary>
    /// Builds a readout from the live session. When the player tank has lost its
    /// <see cref="Gun"/> component (knock-out, despawn) returns a flagged-dead snapshot
    /// instead of throwing — the renderer hides the player chrome in that case.
    /// </summary>
    public static MatchHudReadout Capture(MatchSession session)
    {
        var world = session.World;
        if (!world.Has<SimGun>(session.Player))
        {
            return new MatchHudReadout(
                session.AlivePlayers,
                session.AliveOpponents,
                IsPlayerAlive: false,
                ReloadFraction: 0f,
                ChamberedRound: AmmoType.AP);
        }

        ref var gun = ref world.Get<SimGun>(session.Player);
        return new MatchHudReadout(
            session.AlivePlayers,
            session.AliveOpponents,
            IsPlayerAlive: true,
            ReloadFraction: ReloadProgress.Of(gun.ReloadSeconds, gun.ReloadSecondsMax),
            ChamberedRound: ToContent(gun.Chambered.Type));
    }

    // Sim.AmmoType and Content.AmmoType share numeric values by design (see AmmoType.cs);
    // the cast is the documented contract.
    private static AmmoType ToContent(SimAmmoType type) => (AmmoType)(byte)type;
}
