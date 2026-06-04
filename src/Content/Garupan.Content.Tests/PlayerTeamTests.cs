using System.IO;
using System.Linq;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests;

/// <summary>Roster-shape coverage for the player crew. Validates the
/// CSV-loaded roster against the canon invariants (five members, every role filled
/// exactly once, every member is PlayerSchool). Parser-level rules are pinned by
/// <see cref="Catalogs.CrewRosterCsvTests"/>.</summary>
public sealed class PlayerTeamTests
{
    private static CrewRoster LoadCanonRoster()
    {
        var canonicalCsvPath = Path.Combine(
            System.AppContext.BaseDirectory, "data", "crews", "player_crew.csv");
        return PlayerTeam.Load(canonicalCsvPath);
    }

    [Fact]
    public void Canon_roster_has_five_members()
    {
        LoadCanonRoster().All.Should().HaveCount(5);
    }

    [Fact]
    public void Each_canon_role_filled_exactly_once()
    {
        var roles = LoadCanonRoster().All.Select(m => m.Role).ToHashSet();
        roles.Should().BeEquivalentTo(new[]
        {
            CrewRole.Commander,
            CrewRole.Gunner,
            CrewRole.Loader,
            CrewRole.Driver,
            CrewRole.RadioOperator,
        });
    }

    [Fact]
    public void Commander_is_named_correctly()
    {
        var sample = LoadCanonRoster().FindById("crew_lead");
        sample.Should().NotBeNull();
        sample!.Role.Should().Be(CrewRole.Commander);
        sample.GivenName.Should().Be("Alex");
        sample.FamilyName.Should().Be("Doe");
    }

    [Fact]
    public void All_members_belong_to_player_school()
    {
        var roster = LoadCanonRoster();
        roster.SchoolKey.Should().Be(PlayerTeam.SchoolKey);
        roster.SchoolKey.Should().Be("player_school");
        roster.All.Should().OnlyContain(m => m.SchoolKey == PlayerTeam.SchoolKey);
    }
}
