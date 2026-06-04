using System;
using System.Collections.Generic;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Screens.Lobby;

/// <summary>
/// Owns the geometry of the lobby's mode-card grid: per-card rectangles, hover hit-test,
/// and dispatch to <see cref="MatchModeCardRenderer"/> per card. Kept separate from
/// <see cref="LobbyScreen"/> so the orchestrator stays focused on input + screen
/// transitions while card layout grows alongside future modes without touching that
/// orchestration.
/// </summary>
internal sealed class MatchModeListView
{
    private readonly MatchModeCardRenderer _cardRenderer;
    private readonly List<CardRect> _rects = new();

    public MatchModeListView(MatchModeCardRenderer cardRenderer)
    {
        _cardRenderer = cardRenderer;
    }

    /// <summary>Recomputes per-card rectangles for the current surface width and the
    /// supplied <paramref name="cardCount"/>. Cards are centred horizontally.</summary>
    public void Layout(int surfaceWidth, int cardCount)
    {
        _rects.Clear();
        if (cardCount <= 0)
        {
            return;
        }

        var total = (cardCount * LobbyPalette.CardWidth) +
            (Math.Max(0, cardCount - 1) * LobbyPalette.CardGap);
        var startX = (surfaceWidth - total) / 2;
        for (var i = 0; i < cardCount; i++)
        {
            var x = startX + (i * (LobbyPalette.CardWidth + LobbyPalette.CardGap));
            _rects.Add(new CardRect(x, LobbyPalette.CardGridY, LobbyPalette.CardWidth, LobbyPalette.CardHeight));
        }
    }

    /// <summary>Index of the card under (mouseX, mouseY), or -1 when none.</summary>
    public int HitTest(int mouseX, int mouseY)
    {
        for (var i = 0; i < _rects.Count; i++)
        {
            var r = _rects[i];
            if (mouseX >= r.X && mouseX < r.X + r.Width &&
                mouseY >= r.Y && mouseY < r.Y + r.Height)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Draws every resolved card using its laid-out rect, highlighting the one
    /// at <paramref name="hoveredIndex"/> when non-negative.</summary>
    public void Render(
        IDrawSurface surface,
        IReadOnlyList<ResolvedMatchModeCard> cards,
        int hoveredIndex,
        LobbyTranslations translations)
    {
        for (var i = 0; i < cards.Count && i < _rects.Count; i++)
        {
            _cardRenderer.Render(surface, cards[i], _rects[i], isHovered: i == hoveredIndex, translations);
        }
    }

    internal readonly record struct CardRect(int X, int Y, int Width, int Height);
}
