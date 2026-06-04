namespace Garupan.Sim.Components;

/// <summary>
/// Transient tag attached by <see cref="Systems.RespawnSystem"/> the tick a tank
/// respawns: a brief window during which the tank cannot be re-knocked-out, so a
/// spawn-camper cannot one-shot the returning crew. <see cref="Systems.SpawnInvulnerabilitySystem"/>
/// counts the timer down each tick and removes the tag when it hits zero;
/// <see cref="Systems.ProjectileHitResolveSystem"/> excludes invulnerable tanks from the
/// hit-candidate list, so projectiles pass cleanly through during the window.
/// </summary>
/// <remarks>
/// Sized as a single <see cref="ushort"/>: at 30 Hz a <c>ushort.MaxValue</c> timer is
/// ~36 minutes, far beyond any realistic spawn-shield duration. The component's presence
/// is the signal — the tag is removed entirely when invulnerability ends, so a query
/// for "currently shielded tanks" is a simple <c>WithAll&lt;RespawnInvulnerable&gt;()</c>.
/// </remarks>
public struct RespawnInvulnerable
{
    public ushort TicksRemaining;
}
