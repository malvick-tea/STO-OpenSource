namespace Garupan.Server.Match;

/// <summary>Named defaults referenced by <see cref="MatchHostOptions"/> parameter
/// initialisers. C# positional records can only reference compile-time constants
/// declared outside the record's own body — pulling the values here keeps the
/// named-default discipline without giving up the terse positional record syntax.</summary>
public static class MatchHostDefaults
{
    /// <summary>Five seconds at the canonical 30 Hz tick rate — long enough for the
    /// verdict to read, short enough to recycle a busy server quickly.</summary>
    public const int PostMatchHoldTicks = 150;
}
