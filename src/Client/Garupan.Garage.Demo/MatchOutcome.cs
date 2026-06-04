namespace Garupan.Garage.Demo;

/// <summary>High-level outcome of the demo match, derived once per Sim tick from the
/// player + opponent <c>KnockedOut</c> flags. <see cref="Defeat"/> wins over
/// <see cref="Victory"/> in a simultaneous-KO frame — a canon match wouldn't let both
/// sides win.</summary>
public enum MatchOutcome
{
    InProgress,
    Victory,
    Defeat,
}
