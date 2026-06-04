using System.IO;
using FluentAssertions;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

public sealed class AmmoCsvTests
{
    private const string Header =
        "id,type,muzzle_velocity_mps,mass_kg,penetration_mm,diameter_meters,drag_coefficient," +
        "propellant_charge_mass_kg,gas_velocity_factor";

    [Fact]
    public void Parse_maps_recoil_inputs_from_authoring_data()
    {
        var rounds = AmmoCsv.Parse(Header + "\n" + "round,AP,750,6.8,132,0.075,0.295,2.43,1.5");

        rounds.Should().ContainSingle();
        rounds[0].PropellantChargeMassKg.Should().Be(2.43f);
        rounds[0].GasVelocityFactor.Should().Be(1.5f);
    }

    [Fact]
    public void Parse_rejects_duplicate_ids()
    {
        var row = "round,AP,750,6.8,132,0.075,0.295,2.43,1.5";
        var act = () => AmmoCsv.Parse(Header + "\n" + row + "\n" + row);

        act.Should().Throw<InvalidDataException>().WithMessage("*duplicate*");
    }
}
