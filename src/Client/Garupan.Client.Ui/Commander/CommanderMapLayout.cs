namespace Garupan.Client.Ui.Commander;

/// <summary>
/// Pure geometry for the commander-map screen — top bar, toolbar strip, and the paper
/// surface. The input bridge, the renderer, and the screen's hit-test all consume the
/// same routines so the drawn rectangles and the click regions never drift apart.
/// </summary>
public static class CommanderMapLayout
{
    /// <summary>Map paper region, inset from the surface by a margin around the top bar
    /// and bottom toolbar so paper edges have breathing room.</summary>
    public static CommanderMapBounds Paper(int surfaceWidth, int surfaceHeight) => new(
        CommanderMapPalette.PaperMargin,
        CommanderMapPalette.TopBarHeight + CommanderMapPalette.PaperMargin,
        surfaceWidth - (CommanderMapPalette.PaperMargin * 2),
        surfaceHeight - CommanderMapPalette.TopBarHeight - CommanderMapPalette.ToolbarHeight - (CommanderMapPalette.PaperMargin * 2));

    /// <summary>Top-bar band with the screen title. Full width, fixed height.</summary>
    public static CommanderMapBounds TopBar(int surfaceWidth) =>
        new(0, 0, surfaceWidth, CommanderMapPalette.TopBarHeight);

    /// <summary>Bottom toolbar band. Full width, fixed height, anchored to the bottom.</summary>
    public static CommanderMapBounds Toolbar(int surfaceWidth, int surfaceHeight) =>
        new(0, surfaceHeight - CommanderMapPalette.ToolbarHeight, surfaceWidth, CommanderMapPalette.ToolbarHeight);
}
