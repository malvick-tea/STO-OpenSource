using FluentAssertions;
using Garupan.Client.Core.Application;
using Garupan.Client.Ui.Screens.Settings;
using Opus.Engine.Input;
using Xunit;

namespace Garupan.Client.Ui.Tests.Screens.Settings;

/// <summary>
/// Covers <see cref="ControlsModel"/> — the row cursor, the listening lifecycle, key
/// capture, and the swap-on-conflict policy. Action order: MoveForward, MoveBackward,
/// SteerLeft, SteerRight, Fire (see <see cref="InputActionCatalog"/>).
/// </summary>
public sealed class ControlsModelTests
{
    [Fact]
    public void Starts_on_the_first_row_and_not_listening()
    {
        var model = new ControlsModel(InputBindings.Default);

        model.SelectedRow.Should().Be(0);
        model.IsListening.Should().BeFalse();
        model.Bindings.Should().Be(InputBindings.Default);
    }

    [Fact]
    public void MoveSelection_clamps_to_the_action_bounds()
    {
        var model = new ControlsModel(InputBindings.Default);

        model.MoveSelection(-1);
        model.SelectedRow.Should().Be(0, "the cursor cannot move above the first action");

        for (var i = 0; i < 20; i++)
        {
            model.MoveSelection(+1);
        }

        model.SelectedRow.Should().Be(model.Actions.Count - 1);
    }

    [Fact]
    public void BeginRebind_then_CaptureKey_binds_the_selected_action_and_fires_Changed()
    {
        var model = new ControlsModel(InputBindings.Default);
        InputBindings? captured = null;
        model.Changed += bindings => captured = bindings;

        model.BeginRebind();
        model.CaptureKey(Key.I);

        model.IsListening.Should().BeFalse();
        model.Bindings.MoveForward.Should().Be(Key.I);
        captured.Should().NotBeNull();
        captured!.MoveForward.Should().Be(Key.I);
    }

    [Fact]
    public void SelectRow_then_BeginRebind_targets_the_selected_action()
    {
        var model = new ControlsModel(InputBindings.Default);

        model.SelectRow(model.Actions.Count - 1);
        model.BeginRebind();
        model.CaptureKey(Key.Enter);

        model.Bindings.Fire.Should().Be(Key.Enter);
        model.Bindings.MoveForward.Should().Be(Key.W, "an unrelated action is untouched");
    }

    [Fact]
    public void Capturing_a_key_held_by_another_action_swaps_the_two()
    {
        var model = new ControlsModel(InputBindings.Default); // MoveForward=W, MoveBackward=S

        model.BeginRebind(); // selected row 0 = MoveForward
        model.CaptureKey(Key.S);

        model.Bindings.MoveForward.Should().Be(Key.S);
        model.Bindings.MoveBackward.Should().Be(Key.W, "the conflicting action takes the freed key");
    }

    [Fact]
    public void Capturing_the_rows_current_key_changes_nothing_and_stays_silent()
    {
        var model = new ControlsModel(InputBindings.Default); // MoveForward = W
        var fired = false;
        model.Changed += _ => fired = true;

        model.BeginRebind();
        model.CaptureKey(Key.W);

        model.IsListening.Should().BeFalse("listening ends even on a no-op capture");
        model.Bindings.MoveForward.Should().Be(Key.W);
        fired.Should().BeFalse();
    }

    [Fact]
    public void CaptureKey_is_ignored_when_not_listening()
    {
        var model = new ControlsModel(InputBindings.Default);

        model.CaptureKey(Key.I);

        model.Bindings.Should().Be(InputBindings.Default);
    }

    [Fact]
    public void CancelRebind_leaves_listening_without_touching_the_bindings()
    {
        var model = new ControlsModel(InputBindings.Default);

        model.BeginRebind();
        model.CancelRebind();

        model.IsListening.Should().BeFalse();
        model.Bindings.Should().Be(InputBindings.Default);
    }

    [Fact]
    public void Escape_is_reserved_and_cannot_be_bound()
    {
        var model = new ControlsModel(InputBindings.Default);

        model.BeginRebind();
        model.CaptureKey(Key.Escape);

        model.Bindings.MoveForward.Should().Be(Key.W);
    }

    [Fact]
    public void MoveSelection_is_ignored_while_listening()
    {
        var model = new ControlsModel(InputBindings.Default);

        model.BeginRebind();
        model.MoveSelection(+1);

        model.SelectedRow.Should().Be(0, "the cursor is frozen while a rebind is in progress");
        model.ListeningRow.Should().Be(0);
    }
}
