using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.MainMenu;

/// <summary>
/// Vertical-band nav list — owns the canonical item order, the hit-test against the mouse,
/// and the row rendering. Knows nothing about routing: the caller resolves the activated
/// index against <see cref="MainMenuActions"/>.
///
/// Items are surfaced as <see cref="string"/> ids so the routing switch stays a single
/// place — turning them into an enum would double the bookkeeping for no gain at this
/// scale (six entries) and the routing switch already has a default branch for unknowns.
/// </summary>
public sealed class MainMenuNavList
{
    public static readonly string[] Items =
    {
        "PLAY",
        "GARAGE",
        "CAMPAIGN",
        "BATTLE PLAN",
        "SETTINGS",
        "ARCHIVE",
        "EXIT",
    };

    public int HitTest(int mouseX, int mouseY)
    {
        if (mouseX < MainMenuPalette.ListX - 12 ||
            mouseX > MainMenuPalette.ListX - 12 + MainMenuPalette.RowWidth)
        {
            return -1;
        }

        for (var i = 0; i < Items.Length; i++)
        {
            var rowY = MainMenuPalette.ListY + (i * MainMenuPalette.RowHeight) - 4;
            if (mouseY >= rowY && mouseY < rowY + (MainMenuPalette.RowHeight - 8))
            {
                return i;
            }
        }

        return -1;
    }

    public void Render(IDrawSurface surface, int hoveredIndex)
    {
        for (var i = 0; i < Items.Length; i++)
        {
            var rowY = MainMenuPalette.ListY + (i * MainMenuPalette.RowHeight) - 4;
            var isHovered = i == hoveredIndex;
            var color = isHovered ? MainMenuPalette.Foreground : MainMenuPalette.Dim;

            if (isHovered)
            {
                surface.FillRect(
                    MainMenuPalette.ListX - 12,
                    rowY,
                    MainMenuPalette.RowWidth,
                    MainMenuPalette.RowHeight - 8,
                    MainMenuPalette.Hover);
                surface.FillRect(
                    MainMenuPalette.ListX - 12,
                    rowY,
                    4,
                    MainMenuPalette.RowHeight - 8,
                    MainMenuPalette.Crimson);
            }

            surface.DrawText(
                Items[i],
                MainMenuPalette.ListX,
                MainMenuPalette.ListY + (i * MainMenuPalette.RowHeight),
                MainMenuPalette.LabelFontSize,
                color);
        }
    }
}
