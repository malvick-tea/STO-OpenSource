using Garupan.Content;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Garage;

/// <summary>
/// Bottom-strip crew chip row. Reads names + roles from the injected
/// <see cref="CrewRoster"/> so adding / reordering a member is a CSV-side change. Role
/// labels are still hard-coded English ("Commander", "Gunner", …) — when the garage
/// gets a <c>LocalizationService</c> injection these will resolve through
/// <see cref="CrewMember.RoleKey"/>.
/// </summary>
public sealed class PlayerCrewChipsRenderer
{
    private const int ChipWidth = 130;
    private const int ChipHeight = 64;
    private const int ChipGap = 12;
    private const int BottomInset = 116;

    private readonly CrewRoster _roster;

    public PlayerCrewChipsRenderer(CrewRoster roster)
    {
        _roster = Ensure.NotNull(roster);
    }

    public void Render(IDrawSurface surface)
    {
        var crew = _roster.All;
        var w = surface.Width;
        var h = surface.Height;

        var crewY = h - BottomInset;

        surface.DrawText("PLAYER TEAM", 32, crewY - 22, 14, GaragePalette.Dim);
        surface.FillRect(32, crewY - 4, 80, 2, GaragePalette.Crimson);

        var totalW = ((ChipWidth + ChipGap) * crew.Count) - ChipGap;
        var chipX = (w - totalW) / 2;

        for (var i = 0; i < crew.Count; i++)
        {
            DrawChip(surface, chipX, crewY, crew[i]);
            chipX += ChipWidth + ChipGap;
        }
    }

    private static void DrawChip(IDrawSurface surface, int x, int y, CrewMember member)
    {
        surface.FillRect(x, y, ChipWidth, ChipHeight, GaragePalette.Panel);
        surface.FillRect(x, y, 4, ChipHeight, GaragePalette.Crimson);

        var nameWidth = surface.MeasureText(member.GivenName, 18);
        surface.DrawText(member.GivenName, x + (ChipWidth - nameWidth) / 2, y + 12, 18, GaragePalette.Foreground);

        var roleLabel = RoleLabel(member.Role);
        var roleWidth = surface.MeasureText(roleLabel, 13);
        surface.DrawText(roleLabel, x + (ChipWidth - roleWidth) / 2, y + 38, 13, GaragePalette.Dim);
    }

    private static string RoleLabel(CrewRole role) => role switch
    {
        CrewRole.Commander => "Commander",
        CrewRole.Gunner => "Gunner",
        CrewRole.Loader => "Loader",
        CrewRole.Driver => "Driver",
        CrewRole.RadioOperator => "Radio Op",
        _ => role.ToString(),
    };
}
