using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests;

public sealed class TankRosterTests
{
    [Fact]
    public void Vehicle_medium_a_is_in_roster()
    {
        TankRoster.All.Should().Contain(TankRoster.VehicleMediumA);
    }

    [Fact]
    public void FindById_returns_known_spec()
    {
        var spec = TankRoster.FindById("vehicle_medium_a");
        spec.Should().NotBeNull();
        spec!.Designation.Should().Be("Medium Tank A (late)");
    }

    [Fact]
    public void FindById_returns_null_for_unknown()
    {
        TankRoster.FindById("nonexistent_tank").Should().BeNull();
    }

    [Fact]
    public void Spec_carries_all_subsystems()
    {
        var spec = TankRoster.VehicleMediumA;
        spec.Armor.HullFront.ThicknessMm.Should().BeGreaterThan(0);
        spec.Gun.PenetrationMm.Should().BeGreaterThan(0);
        spec.GunMount.BarrelLengthMeters.Should().BeGreaterThan(0);
        spec.Mobility.MassTonnes.Should().BeGreaterThan(0);
        spec.Mobility.EnginePowerHorsepower.Should().BeGreaterThan(0);
        spec.Mobility.Drive.ForwardGearRatios.Should().NotBeEmpty();
        spec.CrewSize.Should().Be(5);
    }
}
