namespace Garupan.Sim.Protocol;

/// <summary>
/// Server → client notification that the match has ended. Broadcast once, to every peer,
/// on the tick the server's outcome tracker latches a verdict. The frame carries the
/// objective result; each client decides VICTORY vs DEFEAT for itself by comparing
/// <see cref="WinnerNetworkId"/> against its own network id from the welcome handshake.
/// </summary>
/// <param name="Result">Winner or draw — see <see cref="MatchOverResult"/>.</param>
/// <param name="WinnerNetworkId">The winning tank's network id when the match was a
/// free-for-all; <c>0</c> for a team win or a draw.</param>
/// <param name="WinnerTeam">The winning team id when the match was a team match;
/// <c>0</c> (<see cref="Garupan.Sim.Components.Team.None"/>) for a free-for-all win or a
/// draw.</param>
public readonly record struct MatchOverFrame(MatchOverResult Result, uint WinnerNetworkId, byte WinnerTeam);
