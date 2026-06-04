namespace Garupan.Client.Ui.Commander;

/// <summary>
/// The commander's active tool. The selected tool drives the input bridge: pencil and
/// marker both draw <see cref="CommanderMapStroke"/>s (different thickness), token places
/// a <see cref="CommanderMapToken"/> per click. Arrows will join later as a fourth tool
/// with its own stroke-with-arrowhead variant.
/// </summary>
public enum CommanderTool
{
    Pencil = 0,
    Marker = 1,
    Token = 2,
}

/// <summary>
/// Per-tool draw parameters. Centralised so the renderer, the input bridge, and the
/// toolbar all read the same thicknesses — a future "thinner pencil" tweak is one edit.
/// </summary>
internal static class CommanderToolParameters
{
    public const int PencilThicknessPixels = 2;
    public const int MarkerThicknessPixels = 7;
    public const int TokenRadiusPixels = 14;
    public const int TokenOutlineThickness = 2;
    public const int TokenLabelFontSize = 14;
}
