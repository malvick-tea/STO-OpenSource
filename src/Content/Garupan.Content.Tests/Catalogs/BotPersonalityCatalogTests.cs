using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

/// <summary>Catalog-API coverage for <see cref="BotPersonalityCatalog"/> — lookup
/// semantics, fallback shape, and Contains. Parser-level rules are pinned by
/// <see cref="BotPersonalityCsvTests"/>.</summary>
public sealed class BotPersonalityCatalogTests
{
    private const string Header = "school,engage_range_m,throttle_scale,alignment_tolerance_radians,canon_source";

    [Fact]
    public void Resolve_returns_loaded_personality_when_school_is_in_catalog()
    {
        var csv = $"""
            {Header}
            rival_delta,60,0.4,0.045,test
            """;
        var catalog = BotPersonalityCsv.Parse(csv);
        var p = catalog.Resolve(OpponentSchool.RivalDelta);
        p.School.Should().Be(OpponentSchool.RivalDelta);
        p.EngageRangeMeters.Should().Be(60f);
        p.ThrottleScale.Should().Be(0.4f);
        p.AlignmentToleranceRadians.Should().Be(0.045f);
    }

    [Fact]
    public void Resolve_returns_legacy_fallback_when_school_missing_with_requested_school_stamped_on()
    {
        var csv = $"""
            {Header}
            player_school,55,0.55,0.05,test
            """;
        var catalog = BotPersonalityCsv.Parse(csv);
        var p = catalog.Resolve(OpponentSchool.RivalEcho);
        p.School.Should().Be(OpponentSchool.RivalEcho);
        p.EngageRangeMeters.Should().Be(BotPersonality.LegacyFallback.EngageRangeMeters);
        p.ThrottleScale.Should().Be(BotPersonality.LegacyFallback.ThrottleScale);
        p.AlignmentToleranceRadians.Should().Be(BotPersonality.LegacyFallback.AlignmentToleranceRadians);
    }

    [Fact]
    public void Contains_returns_true_for_loaded_school_and_false_for_missing()
    {
        var csv = $"""
            {Header}
            player_school,55,0.55,0.05,test
            """;
        var catalog = BotPersonalityCsv.Parse(csv);
        catalog.Contains(OpponentSchool.PlayerSchool).Should().BeTrue();
        catalog.Contains(OpponentSchool.RivalDelta).Should().BeFalse();
    }

    [Fact]
    public void All_returns_every_loaded_personality()
    {
        var csv = $"""
            {Header}
            player_school,55,0.55,0.05,first
            rival_delta,60,0.4,0.045,second
            rival_echo,90,0.7,0.03,third
            """;
        var catalog = BotPersonalityCsv.Parse(csv);
        catalog.All.Should().HaveCount(3);
    }

    [Fact]
    public void Count_matches_number_of_loaded_rows()
    {
        var csv = $"""
            {Header}
            player_school,55,0.55,0.05,first
            rival_delta,60,0.4,0.045,second
            """;
        var catalog = BotPersonalityCsv.Parse(csv);
        catalog.Count.Should().Be(2);
    }

    [Fact]
    public void LegacyFallback_matches_M3_M4_AiBotSystem_defaults()
    {
        BotPersonality.LegacyFallback.EngageRangeMeters.Should().Be(60f);
        BotPersonality.LegacyFallback.ThrottleScale.Should().Be(0.5f);
        BotPersonality.LegacyFallback.AlignmentToleranceRadians.Should().Be(0.05f);
    }

    [Fact]
    public void Canonical_ai_personalities_csv_covers_every_school()
    {
        var canonicalCsvPath = System.IO.Path.Combine(
            System.AppContext.BaseDirectory, "data", "ai-personalities.csv");
        var catalog = BotPersonalityCsv.LoadFile(canonicalCsvPath);
        foreach (var school in System.Enum.GetValues<OpponentSchool>())
        {
            catalog.Contains(school).Should().BeTrue($"data/ai-personalities.csv should cover {school}");
        }
    }
}
