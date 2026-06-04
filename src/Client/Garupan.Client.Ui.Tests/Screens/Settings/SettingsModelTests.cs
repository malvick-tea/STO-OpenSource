using FluentAssertions;
using Garupan.Client.Core.Application;
using Garupan.Client.Ui.Screens.Settings;
using Xunit;

namespace Garupan.Client.Ui.Tests.Screens.Settings;

/// <summary>
/// Covers <see cref="SettingsModel"/> — option ordering, value cycling / clamping, the
/// selection cursor, and the <see cref="SettingsModel.Changed"/> notification.
/// Option order: VSync, Resolution, Master, Music, Sfx, Locale, Controls link, Multiplayer link.
/// </summary>
public sealed class SettingsModelTests
{
    private const int VSyncRow = 0;
    private const int ResolutionRow = 1;
    private const int MasterRow = 2;
    private const int SfxRow = 4;
    private const int LocaleRow = 5;

    private static readonly string[] Locales = { "en", "ru", "ja" };

    private static SettingsModel ModelFrom(AppSettings settings) => new(settings, Locales);

    [Fact]
    public void Builds_a_row_per_setting_plus_the_navigation_links()
    {
        var model = ModelFrom(AppSettings.Default);

        model.Options.Should().HaveCount(8, "six settings rows plus the Controls + Multiplayer navigation rows");
    }

    [Fact]
    public void Locale_option_is_dropped_when_no_locales_are_available()
    {
        var model = new SettingsModel(AppSettings.Default, System.Array.Empty<string>());

        model.Options.Should().HaveCount(7, "five settings rows plus the Controls + Multiplayer navigation rows");
    }

    [Fact]
    public void Penultimate_row_is_the_controls_navigation_link()
    {
        var model = ModelFrom(AppSettings.Default);

        var link = model.Options[^2].Should().BeOfType<SettingsLinkOption>().Subject;
        link.Target.Should().Be(SettingsLinkTarget.Controls);
    }

    [Fact]
    public void Last_row_is_the_multiplayer_navigation_link()
    {
        var model = ModelFrom(AppSettings.Default);

        var link = model.Options[^1].Should().BeOfType<SettingsLinkOption>().Subject;
        link.Target.Should().Be(SettingsLinkTarget.Multiplayer);
    }

    [Fact]
    public void Adjusting_a_volume_steps_it_by_five_percent()
    {
        var model = ModelFrom(AppSettings.Default with { MasterVolume = 0.50f });
        model.SelectRow(MasterRow);

        model.AdjustSelected(+1);

        model.Current.MasterVolume.Should().BeApproximately(0.55f, 1e-4f);
        model.Options[MasterRow].Display(model.Current).Should().Be("55%");
    }

    [Fact]
    public void A_volume_clamps_at_one_hundred_percent_and_stays_silent()
    {
        var model = ModelFrom(AppSettings.Default with { SfxVolume = 1.0f });
        model.SelectRow(SfxRow);
        var notified = false;
        model.Changed += _ => notified = true;

        model.AdjustSelected(+1);

        model.Current.SfxVolume.Should().Be(1.0f);
        notified.Should().BeFalse("a clamped no-op must not raise Changed");
    }

    [Fact]
    public void A_volume_clamps_at_zero_percent()
    {
        var model = ModelFrom(AppSettings.Default with { MasterVolume = 0f });
        model.SelectRow(MasterRow);

        model.AdjustSelected(-1);

        model.Current.MasterVolume.Should().Be(0f);
    }

    [Fact]
    public void VSync_toggles_between_on_and_off()
    {
        var model = ModelFrom(AppSettings.Default with { VSync = true });
        model.SelectRow(VSyncRow);

        model.AdjustSelected(+1);
        model.Current.VSync.Should().BeFalse();

        model.AdjustSelected(+1);
        model.Current.VSync.Should().BeTrue();
    }

    [Fact]
    public void Locale_cycles_through_every_locale_and_wraps()
    {
        var model = ModelFrom(AppSettings.Default with { Locale = "en" });
        model.SelectRow(LocaleRow);

        model.AdjustSelected(+1);
        model.Current.Locale.Should().Be("ru");

        model.AdjustSelected(+1);
        model.Current.Locale.Should().Be("ja");

        model.AdjustSelected(+1);
        model.Current.Locale.Should().Be("en", "the choice list wraps");
    }

    [Fact]
    public void Resolution_cycles_to_the_next_preset()
    {
        var model = ModelFrom(AppSettings.Default with { WindowWidth = 1280, WindowHeight = 720 });
        model.SelectRow(ResolutionRow);

        model.AdjustSelected(+1);

        model.Current.WindowWidth.Should().Be(1600);
        model.Current.WindowHeight.Should().Be(900);
    }

    [Fact]
    public void MoveSelection_clamps_to_the_option_bounds()
    {
        var model = ModelFrom(AppSettings.Default);

        model.MoveSelection(-1);
        model.SelectedRow.Should().Be(0, "selection cannot move above the first row");

        for (var i = 0; i < 20; i++)
        {
            model.MoveSelection(+1);
        }

        model.SelectedRow.Should().Be(model.Options.Count - 1, "selection cannot move past the last row");
    }

    [Fact]
    public void SelectRow_ignores_out_of_range_indices()
    {
        var model = ModelFrom(AppSettings.Default);
        model.SelectRow(MasterRow);

        model.SelectRow(99);

        model.SelectedRow.Should().Be(MasterRow);
    }

    [Fact]
    public void Changed_carries_the_new_snapshot()
    {
        var model = ModelFrom(AppSettings.Default with { MusicVolume = 0.40f });
        model.SelectRow(3);
        AppSettings? captured = null;
        model.Changed += snapshot => captured = snapshot;

        model.AdjustSelected(+1);

        captured.Should().NotBeNull();
        captured!.MusicVolume.Should().BeApproximately(0.45f, 1e-4f);
    }
}
