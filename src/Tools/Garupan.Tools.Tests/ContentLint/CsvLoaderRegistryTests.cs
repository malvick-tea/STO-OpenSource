using FluentAssertions;
using Garupan.Tools.ContentLint;
using Xunit;

namespace Garupan.Tools.Tests.ContentLint;

/// <summary>Pin the relative-path → loader mapping so a missing entry surfaces
/// immediately. The matchers run on normalised forward-slash paths.</summary>
public sealed class CsvLoaderRegistryTests
{
    [Theory]
    [InlineData("school-palette.csv", "school-palette")]
    [InlineData("ai-personalities.csv", "ai-personalities")]
    [InlineData("garage-demo-match.csv", "garage-demo-match")]
    [InlineData("garage-lighting.csv", "garage-lighting")]
    [InlineData("shell-visuals.csv", "shell-visuals")]
    [InlineData("match-modes.csv", "match-modes")]
    [InlineData("campaigns/sample.csv", "campaign")]
    [InlineData("crews/player_crew.csv", "crew")]
    [InlineData("crews/duck.csv", "crew")]
    public void FindFor_matches_known_paths(string relative, string expectedDescription)
    {
        var match = CsvLoaderRegistry.FindFor(relative);
        match.Should().NotBeNull();
        match!.Description.Should().Be(expectedDescription);
    }

    [Theory]
    [InlineData("unknown.csv")]
    [InlineData("nested/random.csv")]
    [InlineData("school-palette.json")]
    public void FindFor_returns_null_for_unknown_paths(string relative)
    {
        CsvLoaderRegistry.FindFor(relative).Should().BeNull();
    }
}
