using System.Collections.Generic;
using FluentAssertions;
using Garupan.Client.Ui.Screens.Lobby;
using Garupan.Client.Ui.Tests.Fixtures;
using Garupan.Content;
using Xunit;

namespace Garupan.Client.Ui.Tests.Screens.Lobby;

/// <summary>
/// Covers <see cref="MatchModeListView"/> — card-grid layout, hover hit-test, and the
/// dispatch into <see cref="MatchModeCardRenderer"/>. Uses
/// <see cref="RecordingDrawSurface"/> so render assertions stay deterministic.
/// </summary>
public sealed class MatchModeListViewTests
{
    private const int SurfaceWidth = 1280;
    private const int SurfaceHeight = 720;

    [Fact]
    public void HitTest_finds_a_card_under_the_cursor()
    {
        var (view, _) = ListWithCards(2);
        view.Layout(SurfaceWidth, 2);

        var firstCardX = (SurfaceWidth - ((2 * LobbyPalette.CardWidth) + LobbyPalette.CardGap)) / 2;
        view.HitTest(firstCardX + 10, LobbyPalette.CardGridY + 10).Should().Be(0);

        var secondCardX = firstCardX + LobbyPalette.CardWidth + LobbyPalette.CardGap;
        view.HitTest(secondCardX + 10, LobbyPalette.CardGridY + 10).Should().Be(1);
    }

    [Fact]
    public void HitTest_returns_negative_one_outside_any_card()
    {
        var (view, _) = ListWithCards(2);
        view.Layout(SurfaceWidth, 2);

        view.HitTest(10, 10).Should().Be(-1);
        view.HitTest(SurfaceWidth - 10, SurfaceHeight - 10).Should().Be(-1);
    }

    [Fact]
    public void HitTest_in_the_card_gap_returns_negative_one()
    {
        var (view, _) = ListWithCards(2);
        view.Layout(SurfaceWidth, 2);

        var firstCardX = (SurfaceWidth - ((2 * LobbyPalette.CardWidth) + LobbyPalette.CardGap)) / 2;
        var gapCentre = firstCardX + LobbyPalette.CardWidth + (LobbyPalette.CardGap / 2);
        view.HitTest(gapCentre, LobbyPalette.CardGridY + 10).Should().Be(-1);
    }

    [Fact]
    public void Render_clears_nothing_but_does_emit_per_card_geometry()
    {
        var (view, cards) = ListWithCards(2);
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);
        view.Layout(SurfaceWidth, cards.Count);

        view.Render(surface, cards, hoveredIndex: -1, FixtureTranslations());

        // Two cards means at least two card-body FillRects with CardWidth × CardHeight.
        var bodyRectsCount = 0;
        foreach (var cmd in surface.Commands)
        {
            if (cmd is DrawFillRect r &&
                r.W == LobbyPalette.CardWidth &&
                r.H == LobbyPalette.CardHeight)
            {
                bodyRectsCount++;
            }
        }

        bodyRectsCount.Should().Be(2);
    }

    [Fact]
    public void Render_highlights_the_hovered_card_with_a_crimson_border()
    {
        var (view, cards) = ListWithCards(2);
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);
        view.Layout(SurfaceWidth, cards.Count);

        view.Render(surface, cards, hoveredIndex: 0, FixtureTranslations());

        // The hovered card draws its top border in crimson; the unhovered one uses the
        // panel-border colour. Verify the crimson colour appears at least once.
        var hasCrimsonBorder = false;
        foreach (var cmd in surface.Commands)
        {
            if (cmd is DrawFillRect r &&
                r.Color.R == LobbyPalette.Crimson.R &&
                r.Color.G == LobbyPalette.Crimson.G &&
                r.Color.B == LobbyPalette.Crimson.B)
            {
                hasCrimsonBorder = true;
                break;
            }
        }

        hasCrimsonBorder.Should().BeTrue();
    }

    private static (MatchModeListView View, IReadOnlyList<ResolvedMatchModeCard> Cards) ListWithCards(int count)
    {
        var view = new MatchModeListView(new MatchModeCardRenderer());
        var cards = new List<ResolvedMatchModeCard>(count);
        for (var i = 0; i < count; i++)
        {
            var mode = new MatchMode(
                Id: $"mode_{i}",
                Kind: i == 0 ? MatchModeKind.FreeForAll : MatchModeKind.TeamTactical,
                NameKey: $"lobby.mode.test{i}.name",
                SummaryKey: $"lobby.mode.test{i}.summary",
                LobbyCapacity: 10,
                RespawnLimit: i == 0 ? 3 : 1,
                IsCommanderLed: i != 0);
            cards.Add(new ResolvedMatchModeCard(mode, $"Mode {i}", "Short summary."));
        }

        return (view, cards);
    }

    private static LobbyTranslations FixtureTranslations() =>
        new(
            Title: "Play",
            Hint: "Pick a mode. Esc to back out.",
            ClosedAlpha: "Closed alpha — applications via DM.",
            DeployLabel: "Queue",
            RespawnsTemplate: "Respawns: {0}",
            CommanderLed: "Commander-led",
            FreeForAll: "Free-for-all");
}
