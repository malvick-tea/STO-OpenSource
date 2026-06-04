using FluentAssertions;
using Xunit;

namespace Garupan.Content.Tests;

public sealed class CatalogValidatorTests
{
    private const string CanonicalPaletteCsv = """
        school,r,g,b,a,canon_source
        player_school,1.0,1.0,1.0,1.0,test
        rival_alpha,0.95,0.82,0.65,1.0,test
        rival_bravo,1.0,0.96,0.85,1.0,test
        rival_charlie,1.0,0.92,0.62,1.0,test
        rival_delta,0.85,0.88,0.95,1.0,test
        rival_echo,0.55,0.55,0.62,1.0,test
        rival_foxtrot,0.85,0.92,0.58,1.0,test
        bcfreedom,1.0,0.9,0.58,1.0,test
        """;

    [Fact]
    public void Default_catalogue_validates_clean_against_ammo()
    {
        var result = CatalogValidator.Validate();
        result.Ok.Should().BeTrue($"every TankRoster entry should resolve its ammo id; errors: [{string.Join("; ", result.Errors)}]");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Default_catalogue_validates_clean_against_full_school_palette()
    {
        var palette = SchoolPaletteCsv.Parse(CanonicalPaletteCsv);
        var result = CatalogValidator.Validate(palette);

        result.Ok.Should().BeTrue(
            $"every TankRoster entry should resolve its school in the full palette; errors: [{string.Join("; ", result.Errors)}]");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_reports_each_tank_whose_school_is_missing_from_the_palette()
    {
        // Partial palette — only PlayerSchool is loaded. Every non-PlayerSchool roster entry must error.
        const string partial = """
            school,r,g,b,a,canon_source
            player_school,1.0,1.0,1.0,1.0,test
            """;
        var palette = SchoolPaletteCsv.Parse(partial);

        var result = CatalogValidator.Validate(palette);

        result.Ok.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("vehicle_heavy_a") && e.Contains("RivalEcho"));
        result.Errors.Should().Contain(e => e.Contains("vehicle_medium_c") && e.Contains("RivalBravo"));
        result.Errors.Should().Contain(e => e.Contains("vehicle_heavy_c") && e.Contains("RivalAlpha"));
    }

    [Fact]
    public void Validate_with_palette_argument_throws_on_null_palette()
    {
        var act = () => CatalogValidator.Validate(null!);
        act.Should().Throw<System.ArgumentNullException>();
    }
}
