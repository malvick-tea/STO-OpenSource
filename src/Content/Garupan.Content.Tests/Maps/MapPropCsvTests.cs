using System.IO;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Maps;

public sealed class MapPropCsvTests
{
    private const string Header = "kind,x,z,yaw,base_diameter_m,height_m";

    [Fact]
    public void Parses_rows_skipping_blank_and_comment_lines()
    {
        var csv = string.Join(
            '\n',
            "# extracted from city.glb",
            Header,
            "Tree,12.5,-3.0,1.57,0.35,9.0",
            string.Empty,
            "TrafficSign,-4,8,0,0.08,2.4");

        var props = MapPropCsv.Parse(csv);

        props.Should().HaveCount(2);
        props[0].Kind.Should().Be(PropKind.Tree);
        props[0].GroundPosition.X.Should().Be(12.5f);
        props[0].GroundPosition.Y.Should().Be(-3.0f);
        props[0].YawRadians.Should().BeApproximately(1.57f, 1e-4f);
        props[0].BaseDiameterMeters.Should().Be(0.35f);
        props[0].HeightMeters.Should().Be(9.0f);
        props[1].Kind.Should().Be(PropKind.TrafficSign);
    }

    [Fact]
    public void Kind_parsing_is_case_insensitive()
    {
        var props = MapPropCsv.Parse($"{Header}\nlamppost,0,0,0,0.12,5");

        props[0].Kind.Should().Be(PropKind.LampPost);
    }

    [Fact]
    public void Rejects_a_header_mismatch()
    {
        var act = () => MapPropCsv.Parse("kind,x,z\nTree,0,0");

        act.Should().Throw<InvalidDataException>().WithMessage("*header mismatch*");
    }

    [Fact]
    public void Rejects_an_unknown_kind()
    {
        var act = () => MapPropCsv.Parse($"{Header}\nSpaceship,0,0,0,1,1");

        act.Should().Throw<InvalidDataException>().WithMessage("*unknown prop kind*");
    }

    [Fact]
    public void Rejects_a_non_positive_size()
    {
        var act = () => MapPropCsv.Parse($"{Header}\nTree,0,0,0,0,9");

        act.Should().Throw<InvalidDataException>().WithMessage("*base_diameter_m*positive*");
    }

    [Fact]
    public void Rejects_a_non_finite_number()
    {
        var act = () => MapPropCsv.Parse($"{Header}\nTree,NaN,0,0,0.3,9");

        act.Should().Throw<InvalidDataException>().WithMessage("*x*finite*");
    }

    [Fact]
    public void Rejects_a_wrong_column_count()
    {
        var act = () => MapPropCsv.Parse($"{Header}\nTree,0,0,0,0.3");

        act.Should().Throw<InvalidDataException>().WithMessage("*expected 6 columns*");
    }
}
