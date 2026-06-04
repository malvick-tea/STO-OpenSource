using System.IO;
using FluentAssertions;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

public sealed class GunCsvTests
{
    private const string Header =
        "id,caliber,penetration_mm,damage,reload_seconds,rounds_per_minute,default_ammo_id," +
        "recoiling_assembly_mass_kg,maximum_recoil_travel_meters,recoil_brake_force_newtons," +
        "muzzle_brake_efficiency,recoil_return_seconds";

    [Fact]
    public void Parse_maps_recoil_mechanism_from_authoring_data()
    {
        var guns = GunCsv.Parse(Header + "\n" + "gun,75mm,132,110,7.5,8,round,900,0.5,100000,0,0.55");

        guns.Should().ContainSingle();
        guns[0].RecoilingAssemblyMassKg.Should().Be(900);
        guns[0].MaximumRecoilTravelMeters.Should().Be(0.5);
        guns[0].RecoilBrakeForceNewtons.Should().Be(100000);
    }

    [Fact]
    public void Parse_rejects_efficiency_outside_unit_interval()
    {
        var act = () => GunCsv.Parse(
            Header + "\n" + "gun,75mm,132,110,7.5,8,round,900,0.5,100000,1.5,0.55");

        act.Should().Throw<InvalidDataException>().WithMessage("*[0, 1]*");
    }
}
