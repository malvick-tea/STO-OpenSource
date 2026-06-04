using FluentAssertions;
using Garupan.Client.Ui.Match.Network;
using Garupan.Content;
using Garupan.Sim.Protocol;
using Xunit;

namespace Garupan.Client.Ui.Tests.Match.Network;

/// <summary>Unit coverage for <see cref="NetworkMatchModeText"/> — the client-side
/// match-mode-kind translation surface: the lobby's <see cref="MatchModeKind"/> catalogue
/// enum mapped onto the wire <see cref="WelcomeMatchModeKind"/>, and either rendered as a
/// human-readable label.</summary>
public sealed class NetworkMatchModeTextTests
{
    [Fact]
    public void FromContent_maps_team_tactical_to_the_team_tactical_wire_kind()
    {
        NetworkMatchModeText.FromContent(MatchModeKind.TeamTactical)
            .Should().Be(WelcomeMatchModeKind.TeamTactical);
    }

    [Fact]
    public void FromContent_maps_free_for_all_to_the_free_for_all_wire_kind()
    {
        NetworkMatchModeText.FromContent(MatchModeKind.FreeForAll)
            .Should().Be(WelcomeMatchModeKind.FreeForAll);
    }

    [Fact]
    public void Label_names_the_team_tactical_mode()
    {
        NetworkMatchModeText.Label(WelcomeMatchModeKind.TeamTactical).Should().Be("TACTICAL 5v5");
    }

    [Fact]
    public void Label_names_the_free_for_all_mode()
    {
        NetworkMatchModeText.Label(WelcomeMatchModeKind.FreeForAll).Should().Be("HUNGRY BATTLES");
    }
}
