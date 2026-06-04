using Garupan.Content;

namespace Garupan.Client.Ui.Match;

/// <summary>
/// HUD display labels for <see cref="AmmoType"/>. NATO acronyms — universal across
/// locales, so no translation key. Centralised here so the renderer doesn't ship a
/// switch each time.
/// </summary>
internal static class AmmoTypeLabels
{
    public static string Of(AmmoType type) => type switch
    {
        AmmoType.AP   => "AP",
        AmmoType.APCR => "APCR",
        AmmoType.HEAT => "HEAT",
        AmmoType.HE   => "HE",
        _             => "—",
    };
}
