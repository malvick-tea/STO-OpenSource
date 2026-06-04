using Garupan.Client.Core.Services;
using Garupan.Content;
using Garupan.Localisation;

namespace Garupan.Client.Ui.Screens.Campaign.Briefing;

/// <summary>
/// Resolves <see cref="OpponentSchool"/> values to their localised display names.
/// Pulled out of <see cref="MissionBriefingScreen"/> so the switch lives near the
/// localisation key table and not buried in a screen.
/// </summary>
internal static class SchoolNames
{
    public static string Resolve(LocalizationService l10n, OpponentSchool school) => school switch
    {
        OpponentSchool.PlayerSchool       => l10n.T(L10nKeys.Schools.PlayerSchool),
        OpponentSchool.RivalAlpha   => l10n.T(L10nKeys.Schools.RivalAlpha),
        OpponentSchool.RivalBravo     => l10n.T(L10nKeys.Schools.RivalBravo),
        OpponentSchool.RivalCharlie        => l10n.T(L10nKeys.Schools.RivalCharlie),
        OpponentSchool.RivalDelta       => l10n.T(L10nKeys.Schools.RivalDelta),
        OpponentSchool.RivalEcho => l10n.T(L10nKeys.Schools.RivalEcho),
        OpponentSchool.RivalFoxtrot     => l10n.T(L10nKeys.Schools.RivalFoxtrot),
        OpponentSchool.RivalGolf    => l10n.T(L10nKeys.Schools.RivalGolf),
        _                           => school.ToString(),
    };
}
