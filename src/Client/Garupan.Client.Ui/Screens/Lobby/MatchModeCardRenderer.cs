using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Lobby;

/// <summary>
/// Draws one match-mode card: body + accent rail + name + badges (FFA vs. commander-led,
/// respawns) + summary paragraph + disabled CTA strip ("Queue — closed alpha"). One
/// responsibility — turning a <see cref="ResolvedMatchModeCard"/> into pixels on a given
/// rect — kept out of <see cref="MatchModeListView"/> so the grid logic stays focused
/// on layout.
/// </summary>
internal sealed class MatchModeCardRenderer
{
    public void Render(
        IDrawSurface surface,
        ResolvedMatchModeCard card,
        MatchModeListView.CardRect rect,
        bool isHovered,
        LobbyTranslations translations)
    {
        DrawBody(surface, rect, isHovered);
        DrawAccentBar(surface, rect, isHovered);

        var cursorY = rect.Y + LobbyPalette.CardPaddingY;
        cursorY = DrawName(surface, card.Name, rect, cursorY);
        cursorY = DrawBadges(surface, card, translations, rect, cursorY);
        DrawSummary(surface, card.Summary, rect, cursorY);
        DrawCallToAction(surface, translations.DeployLabel, rect);
    }

    private static void DrawBody(IDrawSurface surface, MatchModeListView.CardRect rect, bool isHovered)
    {
        var body = isHovered ? LobbyPalette.CardHover : LobbyPalette.CardBody;
        surface.FillRect(rect.X, rect.Y, rect.Width, rect.Height, body);

        var borderColour = isHovered ? LobbyPalette.Crimson : LobbyPalette.CardBorder;
        var t = LobbyPalette.CardBorderThickness;
        surface.FillRect(rect.X, rect.Y, rect.Width, t, borderColour);
        surface.FillRect(rect.X, rect.Y + rect.Height - t, rect.Width, t, borderColour);
        surface.FillRect(rect.X, rect.Y, t, rect.Height, borderColour);
        surface.FillRect(rect.X + rect.Width - t, rect.Y, t, rect.Height, borderColour);
    }

    private static void DrawAccentBar(IDrawSurface surface, MatchModeListView.CardRect rect, bool isHovered)
    {
        var colour = isHovered ? LobbyPalette.Crimson : LobbyPalette.CardBorder;
        surface.FillRect(
            rect.X + LobbyPalette.CardCornerInset,
            rect.Y + LobbyPalette.CardPaddingY + LobbyPalette.CardNameFontSize + 2,
            rect.Width - (LobbyPalette.CardCornerInset * 2),
            LobbyPalette.CardAccentHeight,
            colour);
    }

    private static int DrawName(
        IDrawSurface surface,
        string name,
        MatchModeListView.CardRect rect,
        int cursorY)
    {
        var x = rect.X + LobbyPalette.CardPaddingX;
        surface.DrawText(name, x, cursorY, LobbyPalette.CardNameFontSize, LobbyPalette.Foreground);
        return cursorY + LobbyPalette.CardNameFontSize + LobbyPalette.CardAccentHeight + 12;
    }

    private static int DrawBadges(
        IDrawSurface surface,
        ResolvedMatchModeCard card,
        LobbyTranslations translations,
        MatchModeListView.CardRect rect,
        int cursorY)
    {
        var x = rect.X + LobbyPalette.CardPaddingX;
        var lineupText = card.Mode.IsCommanderLed ? translations.CommanderLed : translations.FreeForAll;
        var respawnsText = LobbyTranslationsFactory.FormatRespawns(
            translations.RespawnsTemplate, card.Mode.RespawnLimit);

        var bx = x;
        foreach (var text in new[] { lineupText, respawnsText })
        {
            var width = surface.MeasureText(text, LobbyPalette.CardBadgeFontSize) + (LobbyPalette.BadgePaddingX * 2);
            surface.FillRect(bx, cursorY, width, LobbyPalette.BadgeHeight, LobbyPalette.Badge);
            surface.DrawText(
                text,
                bx + LobbyPalette.BadgePaddingX,
                cursorY + ((LobbyPalette.BadgeHeight - LobbyPalette.CardBadgeFontSize) / 2),
                LobbyPalette.CardBadgeFontSize,
                LobbyPalette.BadgeText);
            bx += width + LobbyPalette.BadgeSpacing;
        }

        return cursorY + LobbyPalette.BadgeHeight + 16;
    }

    private static void DrawSummary(
        IDrawSurface surface,
        string summary,
        MatchModeListView.CardRect rect,
        int cursorY)
    {
        var x = rect.X + LobbyPalette.CardPaddingX;
        var maxWidth = rect.Width - (LobbyPalette.CardPaddingX * 2);
        foreach (var line in SummaryLineWrapper.Wrap(surface, summary, maxWidth, LobbyPalette.CardSummaryFontSize))
        {
            surface.DrawText(line, x, cursorY, LobbyPalette.CardSummaryFontSize, LobbyPalette.Dim);
            cursorY += LobbyPalette.CardSummaryLineHeight;
        }
    }

    private static void DrawCallToAction(
        IDrawSurface surface,
        string deployLabel,
        MatchModeListView.CardRect rect)
    {
        var stripY = rect.Y + rect.Height - LobbyPalette.CtaStripHeight - LobbyPalette.CardBorderThickness;
        surface.FillRect(
            rect.X + LobbyPalette.CardBorderThickness,
            stripY,
            rect.Width - (LobbyPalette.CardBorderThickness * 2),
            LobbyPalette.CtaStripHeight,
            LobbyPalette.Badge);
        var width = surface.MeasureText(deployLabel, LobbyPalette.CardCtaFontSize);
        surface.DrawText(
            deployLabel,
            rect.X + ((rect.Width - width) / 2),
            stripY + ((LobbyPalette.CtaStripHeight - LobbyPalette.CardCtaFontSize) / 2),
            LobbyPalette.CardCtaFontSize,
            LobbyPalette.Dim);
    }
}
