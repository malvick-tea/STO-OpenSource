using Garupan.Content;

namespace Garupan.Client.Ui.Match;

/// <summary>
/// Maps an <see cref="OpponentSchool"/> to the vehicle the player's team faces in
/// that match. Phase-0 placeholder roster — RivalEcho fields a heavy tank, RivalBravo
/// fields a medium tank, and every other faction falls back to a medium tank until
/// per-faction rosters ship in the catalogue.
///
/// Lives in <c>Garupan.Client.Ui.Match</c> rather than <c>Opus.Content</c> because the
/// "what does each school field as the screen-time threat?" decision is a Phase-0 match
/// design call, not a static catalogue fact. Real per-mission roster tables (with the
/// flag tank, the supporting tanks, the spawn formations) belong in M5+ alongside ADR-0008
/// scripted-beat work.
/// </summary>
internal static class OpponentTankCatalog
{
    public static TankSpec For(OpponentSchool school) => school switch
    {
        OpponentSchool.RivalEcho => TankRoster.VehicleHeavyA,
        OpponentSchool.RivalBravo     => TankRoster.VehicleMediumC,
        _                           => TankRoster.VehicleMediumB,
    };
}
