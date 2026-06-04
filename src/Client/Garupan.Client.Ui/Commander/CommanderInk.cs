using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Commander;

/// <summary>
/// Selectable ink colours for the commander's pen. Two-colour palette — primary (lines,
/// movement) + accent (objectives, threats). More colours would dilute the visual
/// language of a hand-drawn battle plan; the constraint is intentional, not a placeholder.
/// </summary>
public enum CommanderInk
{
    Primary = 0,
    Accent = 1,
}

internal static class CommanderInkColors
{
    public static Color Of(CommanderInk ink) => ink switch
    {
        CommanderInk.Accent => CommanderMapPalette.InkAccent,
        _                   => CommanderMapPalette.InkPrimary,
    };
}
