namespace Garupan.Content;

/// <summary>
/// The player's default crew. Roster data lives in <c>data/crews/player_crew.csv</c>
/// per ADR-0030; this class is the canonical load site.
/// </summary>
/// <remarks>
/// Additional crews can be added as sibling CSVs. Each becomes a separate
/// <see cref="CrewRoster"/> instance; the team identifier is the CSV filename, the
/// faction key on each row is shared (<see cref="SchoolKey"/>).
/// </remarks>
public static class PlayerTeam
{
    /// <summary>The player faction identifier — matches every row's <c>school_key</c>
    /// in <c>data/crews/player_crew.csv</c>.</summary>
    public const string SchoolKey = "player_school";

    /// <summary>Loads the player crew roster from a CSV. Bundled next to
    /// the host exe at boot; callers resolve the path via <c>IVfs.Realize</c>
    /// (typically <c>res://crews/player_crew.csv</c>).</summary>
    public static CrewRoster Load(string csvPath) => CrewRosterCsv.LoadFile(csvPath);
}
