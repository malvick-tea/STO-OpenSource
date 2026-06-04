using System.IO;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

public sealed class GunMountCsvTests
{
    private const string Header =
        "id,min_pitch_degrees,max_pitch_degrees,trunnion_forward_meters,trunnion_height_meters," +
        "barrel_length_meters";

    [Fact]
    public void Parse_maps_geometry_and_catalogue_loads_embedded_profiles()
    {
        var mounts = GunMountCsv.Parse(Header + "\ntest,-8,20,1.25,1.8,3.1");

        mounts.Should().ContainSingle();
        mounts[0].Id.Should().Be("test");
        mounts[0].BarrelLengthMeters.Should().Be(3.1);
        GunMountCatalog.MountMediumA.Id.Should().Be("mount_medium_a");
    }

    [Fact]
    public void Parse_rejects_invalid_pitch_range()
    {
        var act = () => GunMountCsv.Parse(Header + "\ntest,20,-8,1.25,1.8,3.1");

        act.Should().Throw<InvalidDataException>().WithMessage("*pitch range*");
    }
}
