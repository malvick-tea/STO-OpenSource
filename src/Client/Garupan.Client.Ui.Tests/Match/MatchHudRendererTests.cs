using System;
using System.Linq;
using FluentAssertions;
using Garupan.Client.Core.Application;
using Garupan.Client.Ui.Match;
using Garupan.Client.Ui.Tests.Fixtures;
using Garupan.Content;
using Xunit;

namespace Garupan.Client.Ui.Tests.Match;

/// <summary>
/// Covers <see cref="MatchHudRenderer"/> — the chrome layout against a recording surface.
/// The renderer is exercised with hand-built <see cref="MatchHudReadout"/> snapshots so
/// the tests stay ECS-free. Reticle and reload-bar pixel maths live in their own files
/// (<see cref="MatchHudReticleTests"/>, <see cref="ReloadProgressTests"/>).
/// </summary>
public sealed class MatchHudRendererTests
{
    private const int SurfaceWidth = 1280;
    private const int SurfaceHeight = 720;
    private static readonly MatchViewport DefaultViewport = MatchViewport.Fit(SurfaceWidth, SurfaceHeight, 80f);
    private static readonly ReticleAim DefaultAim = new(400, 300, 500, 350, 25f);

    [Fact]
    public void Renders_the_top_bar_with_the_mission_label()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);

        NewRenderer().Render(surface, ReadyReadout(), DefaultViewport, in DefaultAim, showReticle: false);

        var topBarTitle = surface.Commands.OfType<DrawTextCommand>()
            .FirstOrDefault(t => t.Text.StartsWith("MATCH", StringComparison.Ordinal));
        topBarTitle.Should().NotBeNull();
        topBarTitle!.Text.Should().Contain("E5", "the mission's episode reference is in the top bar");
    }

    [Fact]
    public void Renders_the_alive_counts_with_team_colours()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);
        var readout = ReadyReadout() with { AlivePlayers = 3, AliveOpponents = 4 };

        NewRenderer().Render(surface, readout, DefaultViewport, in DefaultAim, showReticle: false);

        var allies = surface.Commands.OfType<DrawTextCommand>()
            .Single(t => t.Text.StartsWith("Allies", StringComparison.Ordinal));
        var enemies = surface.Commands.OfType<DrawTextCommand>()
            .Single(t => t.Text.StartsWith("Enemies", StringComparison.Ordinal));
        allies.Text.Should().Contain("3");
        allies.Color.Should().Be(MatchPalette.PlayerTeam);
        enemies.Text.Should().Contain("4");
        enemies.Color.Should().Be(MatchPalette.OpponentTeam);
    }

    [Fact]
    public void Renders_the_ammo_chip_with_the_chambered_acronym()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);
        var readout = ReadyReadout() with { ChamberedRound = AmmoType.APCR };

        NewRenderer().Render(surface, readout, DefaultViewport, in DefaultAim, showReticle: false);

        var roundHeader = surface.Commands.OfType<DrawTextCommand>().Single(t => t.Text == "ROUND");
        roundHeader.Color.Should().Be(MatchPalette.Dim);
        var chip = surface.Commands.OfType<DrawTextCommand>().Single(t => t.Text == "APCR");
        chip.Color.Should().Be(MatchPalette.AmmoLabel);
    }

    [Fact]
    public void Renders_a_READY_label_when_the_reload_is_complete()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);

        NewRenderer().Render(surface, ReadyReadout(), DefaultViewport, in DefaultAim, showReticle: false);

        surface.Commands.OfType<DrawTextCommand>().Should().Contain(t => t.Text == "READY");
    }

    [Fact]
    public void Renders_a_seconds_remaining_label_while_reloading()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);
        var readout = ReadyReadout() with { ReloadFraction = 0.5f };

        NewRenderer().Render(surface, readout, DefaultViewport, in DefaultAim, showReticle: false);

        var reloadLabel = surface.Commands.OfType<DrawTextCommand>()
            .Single(t => t.Text.EndsWith('s') && t.Text != "ROUND");
        reloadLabel.Text.Should().Be("2.0s");
    }

    [Fact]
    public void Hides_the_ammo_chip_and_shows_KNOCKED_OUT_when_the_player_is_dead()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);
        var readout = ReadyReadout() with { IsPlayerAlive = false };

        NewRenderer().Render(surface, readout, DefaultViewport, in DefaultAim, showReticle: false);

        surface.Commands.OfType<DrawTextCommand>().Should().Contain(t => t.Text == "KNOCKED OUT");
        surface.Commands.OfType<DrawTextCommand>().Should().NotContain(t => t.Text == "ROUND");
        surface.Commands.OfType<DrawTextCommand>().Should().NotContain(t => t.Text == "READY");
    }

    [Fact]
    public void Skips_the_reticle_when_showReticle_is_false()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);

        NewRenderer().Render(surface, ReadyReadout(), DefaultViewport, in DefaultAim, showReticle: false);

        surface.Commands.OfType<DrawTextCommand>().Should().NotContain(
            t => t.Text.EndsWith(" m", StringComparison.Ordinal),
            "the range label is part of the reticle and must be absent");
    }

    [Fact]
    public void Renders_the_reticle_when_showReticle_is_true()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);

        NewRenderer().Render(surface, ReadyReadout(), DefaultViewport, in DefaultAim, showReticle: true);

        surface.Commands.OfType<DrawTextCommand>().Should().Contain(t => t.Text == "25 m");
    }

    [Fact]
    public void Hint_line_includes_the_active_fire_binding()
    {
        var surface = new RecordingDrawSurface(SurfaceWidth, SurfaceHeight);
        var bindings = InputBindings.Default with { Fire = Opus.Engine.Input.Key.Enter };
        var renderer = new MatchHudRenderer("E5", bindings);

        renderer.Render(surface, ReadyReadout(), DefaultViewport, in DefaultAim, showReticle: false);

        var hint = surface.Commands.OfType<DrawTextCommand>().Single(t => t.Text.Contains("drive"));
        hint.Text.Should().Contain("Enter fire");
    }

    private static MatchHudRenderer NewRenderer() => new("E5", InputBindings.Default);

    private static MatchHudReadout ReadyReadout() => new(1, 2, IsPlayerAlive: true, ReloadFraction: 1f, AmmoType.AP);
}
