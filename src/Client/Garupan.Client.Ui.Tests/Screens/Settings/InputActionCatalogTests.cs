using FluentAssertions;
using Garupan.Client.Core.Application;
using Garupan.Client.Ui.Screens.Settings;
using Opus.Engine.Input;
using Xunit;

namespace Garupan.Client.Ui.Tests.Screens.Settings;

/// <summary>Covers <see cref="InputActionCatalog"/> — the rebindable-action catalogue and
/// its read / rebind round trip against <see cref="InputBindings"/>.</summary>
public sealed class InputActionCatalogTests
{
    [Fact]
    public void Catalogue_lists_the_five_rebindable_actions()
    {
        InputActionCatalog.All.Should().HaveCount(5);
    }

    [Fact]
    public void Each_entry_reads_the_default_binding_it_describes()
    {
        var defaults = InputBindings.Default;

        InputActionCatalog.All[0].Read(defaults).Should().Be(Key.W, "the first action is Move forward");
        InputActionCatalog.All[4].Read(defaults).Should().Be(Key.Space, "the last action is Fire");
    }

    [Fact]
    public void Rebind_then_Read_round_trips_a_new_key()
    {
        var entry = InputActionCatalog.All[0];

        var rebound = entry.Rebind(InputBindings.Default, Key.J);

        entry.Read(rebound).Should().Be(Key.J);
    }
}
