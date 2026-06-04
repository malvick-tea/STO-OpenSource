using FluentAssertions;
using Garupan.Client.Core.Application;
using Garupan.Client.Ui.Screens.Settings.Multiplayer;
using Xunit;

namespace Garupan.Client.Ui.Tests.Screens.Settings.Multiplayer;

/// <summary>
/// Pure data-model coverage for <see cref="MultiplayerSettingsModel"/>: field-typing,
/// cursor navigation, focus switching, Commit semantics, port clamping. Each test is a
/// contract sentence — the model never persists or pumps a screen.
/// </summary>
public sealed class MultiplayerSettingsModelTests
{
    private static MultiplayerSettings Initial(string host = "127.0.0.1", int port = 7777) =>
        new() { Host = host, Port = port };

    [Fact]
    public void New_model_seeds_fields_from_initial_settings()
    {
        var model = new MultiplayerSettingsModel(Initial("server.example", 9000));

        model.Host.Value.Should().Be("server.example");
        model.Port.Value.Should().Be("9000");
        model.Current.Host.Should().Be("server.example");
        model.Current.Port.Should().Be(9000);
        model.SelectedField.Should().Be(MultiplayerSettingsModel.HostFieldIndex);
    }

    [Fact]
    public void Typing_into_focused_host_field_appends()
    {
        var model = new MultiplayerSettingsModel(Initial(host: string.Empty));

        model.Type('h');
        model.Type('o');
        model.Type('s');
        model.Type('t');

        model.Host.Value.Should().Be("host");
    }

    [Fact]
    public void Typing_does_not_raise_Changed_until_commit()
    {
        var model = new MultiplayerSettingsModel(Initial());
        var events = 0;
        model.Changed += _ => events++;

        model.Type('a');
        model.Type('b');

        events.Should().Be(0, "typing is mid-edit — Changed must fire only at coherent commits");
    }

    [Fact]
    public void Commit_raises_Changed_once_with_the_new_snapshot()
    {
        var model = new MultiplayerSettingsModel(Initial());
        MultiplayerSettings? last = null;
        var events = 0;
        model.Changed += s =>
        {
            events++;
            last = s;
        };

        model.Type('x');
        model.Commit();

        events.Should().Be(1);
        last!.Host.Should().Be("127.0.0.1x");
    }

    [Fact]
    public void Commit_is_a_no_op_when_the_snapshot_did_not_move()
    {
        var model = new MultiplayerSettingsModel(Initial());
        var events = 0;
        model.Changed += _ => events++;

        model.Commit();

        events.Should().Be(0);
    }

    [Fact]
    public void MoveSelection_commits_pending_edits_before_switching_focus()
    {
        var model = new MultiplayerSettingsModel(Initial());
        var events = 0;
        model.Changed += _ => events++;

        model.Type('y');
        model.MoveSelection(+1);

        events.Should().Be(1);
        model.SelectedField.Should().Be(MultiplayerSettingsModel.PortFieldIndex);
        model.Current.Host.Should().Be("127.0.0.1y");
    }

    [Fact]
    public void MoveSelection_clamps_within_field_range()
    {
        var model = new MultiplayerSettingsModel(Initial());

        // Low end: stepping before the first field stays on it.
        model.MoveSelection(-1);
        model.SelectedField.Should().Be(MultiplayerSettingsModel.HostFieldIndex);

        // High end: stepping past the last field stays on it. The model stacks three
        // endpoint sections (default + Hungry + Tactical) × 2 fields, so the final
        // field is the Tactical port.
        for (var i = 0; i < MultiplayerSettingsModel.FieldCount; i++)
        {
            model.MoveSelection(+1);
        }

        model.SelectedField.Should().Be(MultiplayerSettingsModel.TacticalPortFieldIndex);
    }

    [Fact]
    public void SelectField_switches_focus_when_index_is_valid()
    {
        var model = new MultiplayerSettingsModel(Initial());

        model.SelectField(MultiplayerSettingsModel.PortFieldIndex);

        model.SelectedField.Should().Be(MultiplayerSettingsModel.PortFieldIndex);
    }

    [Fact]
    public void SelectField_ignores_out_of_range_indexes()
    {
        var model = new MultiplayerSettingsModel(Initial());

        model.SelectField(-1);
        model.SelectField(MultiplayerSettingsModel.FieldCount);

        model.SelectedField.Should().Be(MultiplayerSettingsModel.HostFieldIndex);
    }

    [Fact]
    public void Backspace_deletes_in_focused_field()
    {
        var model = new MultiplayerSettingsModel(Initial("abc", 7777));

        model.Backspace();

        model.Host.Value.Should().Be("ab");
    }

    [Fact]
    public void Commit_with_empty_host_falls_back_to_loopback_default()
    {
        var model = new MultiplayerSettingsModel(Initial("abc", 7777));

        model.Backspace();
        model.Backspace();
        model.Backspace();
        model.Commit();

        model.Current.Host.Should().Be(MultiplayerSettings.LoopbackHost);
    }

    [Fact]
    public void Port_field_clamps_to_legal_range_on_commit()
    {
        var model = new MultiplayerSettingsModel(Initial(port: 7777));
        model.MoveSelection(+1);
        for (var i = 0; i < 4; i++)
        {
            model.Backspace();
        }

        // Type a value above the legal port range — the model clamps on commit.
        model.Type('9');
        model.Type('9');
        model.Type('9');
        model.Type('9');
        model.Type('9');
        model.Commit();

        model.Current.Port.Should().Be(MultiplayerSettings.MaxPort);
    }

    [Fact]
    public void Port_field_falls_back_to_default_when_emptied()
    {
        var model = new MultiplayerSettingsModel(Initial(port: 5000));
        model.MoveSelection(+1);
        for (var i = 0; i < 4; i++)
        {
            model.Backspace();
        }

        model.Commit();

        model.Current.Port.Should().Be(MultiplayerSettings.DefaultPort);
    }

    [Fact]
    public void Reset_rolls_back_to_a_provided_snapshot_silently()
    {
        var model = new MultiplayerSettingsModel(Initial("abc", 7777));
        var events = 0;
        model.Changed += _ => events++;
        model.Type('z');

        model.Reset(new MultiplayerSettings { Host = "rolled.back", Port = 9999 });

        model.Host.Value.Should().Be("rolled.back");
        model.Port.Value.Should().Be("9999");
        events.Should().Be(0, "Reset is for cancel paths — it never fires Changed");
    }

    [Fact]
    public void IsValid_is_false_for_an_empty_host()
    {
        var model = new MultiplayerSettingsModel(Initial("a", 7777));
        model.Backspace();

        model.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_is_true_for_default_settings()
    {
        var model = new MultiplayerSettingsModel(Initial());

        model.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_tolerates_an_empty_port_field_during_typing()
    {
        var model = new MultiplayerSettingsModel(Initial("host", 7777));
        model.MoveSelection(+1);
        for (var i = 0; i < 4; i++)
        {
            model.Backspace();
        }

        model.IsValid.Should().BeTrue(
            "an empty port field is a mid-typing state; commit substitutes the default port");
    }
}
