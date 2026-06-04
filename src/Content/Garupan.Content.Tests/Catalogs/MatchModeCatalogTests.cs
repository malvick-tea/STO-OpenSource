using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

/// <summary>API-level coverage for <see cref="MatchModeCatalog"/>. The CSV parser tests
/// in <c>MatchModeCsvTests</c> pin the schema; this fixture pins the lookup semantics
/// — declaration order preserved, id lookups, and contains-checks.</summary>
public sealed class MatchModeCatalogTests
{
    [Fact]
    public void Find_returns_a_known_mode()
    {
        var catalog = MatchModeCsv.Parse(
            "id,kind,name_key,summary_key,lobby_capacity,respawn_limit,commander_led\n" +
            "hungry_battles,FreeForAll,n,s,20,3,false\n");

        catalog.Find("hungry_battles").Should().NotBeNull();
        catalog.Find("hungry_battles")!.LobbyCapacity.Should().Be(20);
    }

    [Fact]
    public void Find_returns_null_for_unknown_id()
    {
        var catalog = MatchModeCsv.Parse(
            "id,kind,name_key,summary_key,lobby_capacity,respawn_limit,commander_led\n" +
            "tactical_5v5,TeamTactical,n,s,10,1,true\n");

        catalog.Find("hungry_battles").Should().BeNull();
        catalog.Contains("hungry_battles").Should().BeFalse();
    }

    [Fact]
    public void Modes_property_lists_modes_in_csv_order()
    {
        var catalog = MatchModeCsv.Parse(
            "id,kind,name_key,summary_key,lobby_capacity,respawn_limit,commander_led\n" +
            "hungry_battles,FreeForAll,n,s,20,3,false\n" +
            "tactical_5v5,TeamTactical,a,b,10,1,true\n");

        catalog.Modes.Should().HaveCount(2);
        catalog.Modes[0].Id.Should().Be("hungry_battles");
        catalog.Modes[1].Id.Should().Be("tactical_5v5");
    }
}
