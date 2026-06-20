namespace Garupan.Server.Match;

internal enum MatchAdmissionDecision
{
    Allowed,
    CapacityReached,
    LateJoinDisabled,
}

internal sealed class MatchAdmissionPolicy
{
    private readonly int _maximumPlayers;
    private readonly bool _allowLateJoin;
    private bool _wasContested;

    public MatchAdmissionPolicy(int maximumPlayers, bool allowLateJoin)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPlayers);
        _maximumPlayers = maximumPlayers;
        _allowLateJoin = allowLateJoin;
    }

    public void ObservePlayerCount(int playerCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(playerCount);
        _wasContested |= playerCount >= 2;
    }

    public MatchAdmissionDecision Evaluate(int currentPlayerCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentPlayerCount);
        if (currentPlayerCount >= _maximumPlayers)
        {
            return MatchAdmissionDecision.CapacityReached;
        }

        return _wasContested && !_allowLateJoin
            ? MatchAdmissionDecision.LateJoinDisabled
            : MatchAdmissionDecision.Allowed;
    }

    public void Reset() => _wasContested = false;
}
