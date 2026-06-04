using System.IO;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

public sealed class GroundDriveCsvTests
{
    private const string Header =
        "id,forward_gear_ratios,reverse_gear_ratio,final_drive_ratio,torque_idle_rpm,torque_peak_rpm," +
        "torque_redline_rpm,idle_rpm,upshift_rpm,downshift_rpm,engine_braking_rate_per_second," +
        "maximum_hull_traverse_radians_per_second,turning_resistance_coefficient_seconds";

    [Fact]
    public void Parse_maps_gears_and_catalogue_loads_embedded_profiles()
    {
        var drives = GroundDriveCsv.Parse(Header + "\ntest,9.4|4.2|2.7,6.2,3.3,600,1600,3000,600,2600,1100,0.18,0.5,0.18");

        drives.Should().ContainSingle();
        drives[0].Id.Should().Be("test");
        drives[0].ForwardGearRatios.Should().Equal(9.4, 4.2, 2.7);
        GroundDriveCatalog.GermanMedium.Id.Should().Be("german_medium");
    }

    [Fact]
    public void Parse_rejects_inconsistent_rpm_range()
    {
        var act = () => GroundDriveCsv.Parse(Header + "\ntest,9.4|4.2,6.2,3.3,600,1600,3000,600,1000,1100,0.18,0.5,0.18");

        act.Should().Throw<InvalidDataException>().WithMessage("*RPM ranges*");
    }
}
