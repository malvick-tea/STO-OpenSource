using FluentAssertions;
using Garupan.Client.Core.Application;
using Garupan.Client.Ui.Match;
using Garupan.Client.Ui.Tests.Fixtures;
using Opus.Engine.Input;
using Xunit;

namespace Garupan.Client.Ui.Tests.Match;

/// <summary>
/// Verifies <see cref="PlayerMovementIntent.Read"/> resolves one keyboard frame against
/// <see cref="InputBindings"/>: the default WASD layout, rebound keys, opposing-key
/// cancellation, and the edge-triggered fire.
/// </summary>
public sealed class PlayerMovementIntentTests
{
    private static readonly InputBindings Defaults = InputBindings.Default;

    [Fact]
    public void No_keys_held_resolves_to_a_neutral_frame()
    {
        var intent = PlayerMovementIntent.Read(new FakeInputSource(), Defaults);

        intent.Should().Be(new PlayerMovementIntent(0f, 0f, false));
    }

    [Fact]
    public void Default_bindings_map_WASD_to_drive()
    {
        PlayerMovementIntent.Read(new FakeInputSource().Hold(Key.W), Defaults).Throttle.Should().Be(1f);
        PlayerMovementIntent.Read(new FakeInputSource().Hold(Key.S), Defaults).Throttle.Should().Be(-1f);
        PlayerMovementIntent.Read(new FakeInputSource().Hold(Key.A), Defaults).Steering.Should().Be(-1f);
        PlayerMovementIntent.Read(new FakeInputSource().Hold(Key.D), Defaults).Steering.Should().Be(1f);
    }

    [Fact]
    public void Opposing_keys_held_together_cancel_to_zero()
    {
        var intent = PlayerMovementIntent.Read(new FakeInputSource().Hold(Key.W, Key.S, Key.A, Key.D), Defaults);

        intent.Throttle.Should().Be(0f);
        intent.Steering.Should().Be(0f);
    }

    [Fact]
    public void Rebound_keys_are_honored_and_the_old_default_goes_dead()
    {
        var rebound = Defaults with { MoveForward = Key.I };

        PlayerMovementIntent.Read(new FakeInputSource().Hold(Key.I), rebound).Throttle.Should().Be(1f);
        PlayerMovementIntent.Read(new FakeInputSource().Hold(Key.W), rebound).Throttle.Should().Be(0f);
    }

    [Fact]
    public void Fire_is_edge_triggered_not_level_triggered()
    {
        PlayerMovementIntent.Read(new FakeInputSource().Press(Key.Space), Defaults).Fire.Should().BeTrue();
        PlayerMovementIntent.Read(new FakeInputSource().Hold(Key.Space), Defaults).Fire.Should().BeFalse();
    }

    [Fact]
    public void Fire_follows_the_rebound_key()
    {
        var rebound = Defaults with { Fire = Key.Enter };

        PlayerMovementIntent.Read(new FakeInputSource().Press(Key.Enter), rebound).Fire.Should().BeTrue();
        PlayerMovementIntent.Read(new FakeInputSource().Press(Key.Space), rebound).Fire.Should().BeFalse();
    }
}
