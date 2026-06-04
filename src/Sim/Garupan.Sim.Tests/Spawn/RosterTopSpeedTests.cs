using System;
using FluentAssertions;
using Garupan.Content;
using Garupan.Sim.Spawn;
using Opus.Engine.Physics.Ground;
using Xunit;

namespace Garupan.Sim.Tests.Spawn;

/// <summary>
/// Pins every roster tank's power-limited terminal road speed on the in-game compacted-earth
/// surface so a physics or catalogue change that silently shifts mobility fails loud. The bands
/// track each chassis's historical road figure, hit through its per-tank <c>rolling_resistance</c>
/// in <c>data/tanks.csv</c> (the medium tank ~38, HeavyA ~38, MediumC ~44, medium tank E ~53, heavy tank D ~27, MediumF ~48
/// km/h). An intentional mobility tune re-pins the affected band like a determinism golden.
/// </summary>
public sealed class RosterTopSpeedTests
{
    private const float Tick = 1f / 60f;
    private const float BandHalfWidthMps = 0.5f;
    private static readonly GroundVehicleEnvironment Ground = GroundVehicleEnvironment.EarthCompactedGround;

    [Theory]
    [InlineData("vehicle_medium_a", 10.7)]
    [InlineData("vehicle_medium_b", 10.0)]
    [InlineData("vehicle_heavy_a", 10.6)]
    [InlineData("vehicle_medium_c", 12.1)]
    [InlineData("vehicle_light_a", 6.8)]
    [InlineData("vehicle_assault_a", 10.7)]
    [InlineData("vehicle_medium_d", 12.0)]
    [InlineData("vehicle_heavy_b", 8.3)]
    [InlineData("vehicle_heavy_c", 6.8)]
    [InlineData("vehicle_medium_e", 14.6)]
    [InlineData("vehicle_heavy_d", 7.4)]
    [InlineData("vehicle_medium_f", 13.5)]
    public void Tank_tops_out_at_its_pinned_terminal_speed(string tankId, double expectedMps)
    {
        var tank = TankRoster.RequireById(tankId);

        var peak = TerminalSpeedMps(GroundVehiclePhysicsFactory.Build(tank.Mobility));

        peak.Should().BeInRange(
            (float)expectedMps - BandHalfWidthMps,
            (float)expectedMps + BandHalfWidthMps,
            $"{tankId} terminal speed moved — re-pin only after an intentional mobility tune");
    }

    [Fact]
    public void Every_roster_tank_has_a_pinned_top_speed()
    {
        // A new chassis must arrive with a speed band above, or this guard fails — the roster's
        // mobility regression net never silently goes stale as the catalogue grows.
        var pinned = typeof(RosterTopSpeedTests)
            .GetMethod(nameof(Tank_tops_out_at_its_pinned_terminal_speed))!
            .GetCustomAttributes(typeof(InlineDataAttribute), inherit: false).Length;

        pinned.Should().Be(TankRoster.All.Count);
    }

    private static float TerminalSpeedMps(GroundVehicleProperties vehicle)
    {
        var state = GroundVehicleState.Rest();
        var peak = 0f;
        var fullThrottle = new GroundVehicleControls(1f, 0f);
        for (var i = 0; i < 60 * 60; i++)
        {
            state = GroundVehicleIntegrator.Advance(state, vehicle, Ground, fullThrottle, Tick);
            peak = MathF.Max(peak, state.VelocityMps.Length());
        }

        return peak;
    }
}
