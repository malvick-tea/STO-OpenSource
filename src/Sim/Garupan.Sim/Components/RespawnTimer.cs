namespace Garupan.Sim.Components;

/// <summary>
/// Transient component attached by <see cref="Systems.RespawnSystem"/> the first tick a
/// <see cref="KnockedOut"/> tank still has respawn budget. Counts down each tick; when
/// <see cref="TicksRemaining"/> reaches zero the system removes the <see cref="KnockedOut"/>
/// tag, resets the tank's <see cref="Transform"/> to its <see cref="RespawnLives"/> anchor,
/// and removes this timer.
/// </summary>
/// <remarks>
/// The component's presence is the signal that a tank is "respawning, not finally dead" —
/// the outcome tracker uses it to keep the participant in the match while the timer runs.
/// Sized as a single <see cref="ushort"/>: at 30 Hz a <c>ushort.MaxValue</c> timer is
/// ~36 minutes, far beyond any realistic respawn delay.
/// </remarks>
public struct RespawnTimer
{
    public ushort TicksRemaining;
}
