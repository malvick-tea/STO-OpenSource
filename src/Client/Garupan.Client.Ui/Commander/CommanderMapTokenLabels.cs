using System.Globalization;

namespace Garupan.Client.Ui.Commander;

/// <summary>
/// Auto-labels new tokens with the next sequential integer based on how many tokens are
/// already on the map. The commander's tactical sketch reads naturally as "1, 2, 3" —
/// numbered units in placement order, the same shorthand a real-paper map would use.
///
/// Sequential, not gap-aware: undoing token "3" and placing a new one yields "3" again
/// because there are still only 2 tokens at the moment of placement. A future
/// commander-typed label affordance can replace this when text input lands.
/// </summary>
public static class CommanderMapTokenLabels
{
    /// <summary>The label to assign to the next token placed onto <paramref name="state"/>.</summary>
    public static string Next(CommanderMapState state)
    {
        var nextIndex = state.Tokens.Count + 1;
        return nextIndex.ToString(CultureInfo.InvariantCulture);
    }
}
