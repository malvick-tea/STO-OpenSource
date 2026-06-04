using System;
using System.Numerics;
using FluentAssertions;
using Garupan.Content;
using Garupan.Sim.Spawn;
using Opus.Engine.Physics.Ground;
using Xunit;

namespace Garupan.Sim.Tests.Spawn;

/// <summary>
/// Pins the tracked-vehicle feel the catalogue adapter must produce: a historical road speed
/// (power-limited, not the ~62 km/h a wheeled rolling coefficient once allowed) and a prompt
/// off-throttle coast (engine braking, not a hundreds-of-metres glide). Both are the physics
/// the player reported as wrong, so they are locked here against the real the medium tank catalogue entry.
/// </summary>
public sealed class GroundVehiclePhysicsFactoryTests
{
    private const float Tick = 1f / 60f;
    private static readonly GroundVehicleEnvironment Ground = GroundVehicleEnvironment.EarthCompactedGround;

    [Fact]
    public void Pz_iv_tops_out_at_a_historical_road_speed()
    {
        var vehicle = GroundVehiclePhysicsFactory.Build(TankRoster.VehicleMediumB.Mobility);

        var peak = 0f;
        var state = GroundVehicleState.Rest();
        for (var i = 0; i < 60 * 40; i++)
        {
            state = GroundVehicleIntegrator.Advance(state, vehicle, Ground, new GroundVehicleControls(1f, 0f), Tick);
            peak = MathF.Max(peak, state.VelocityMps.Length());
        }

        // 9–12 m/s ≈ 32–43 km/h brackets the medium tank's ~38 km/h road speed and rejects the
        // pre-fix ~62 km/h (17 m/s) that the wheeled-vehicle rolling coefficient produced.
        peak.Should().BeInRange(9f, 12f);
    }

    [Fact]
    public void Lifting_the_throttle_coasts_to_rest_in_tens_of_metres()
    {
        var vehicle = GroundVehiclePhysicsFactory.Build(TankRoster.VehicleMediumB.Mobility);

        var state = Drive(GroundVehicleState.Rest(), vehicle, throttle: 1f, seconds: 20f);
        state.VelocityMps.Length().Should().BeGreaterThan(8f, "the tank must be at cruise before it coasts");

        var start = state.PositionMeters;
        state = Drive(state, vehicle, throttle: 0f, seconds: 20f);

        state.VelocityMps.Length().Should().BeLessThan(0.5f, "engine braking must shed the cruise, not glide on");
        Vector2.Distance(state.PositionMeters, start).Should()
            .BeLessThan(60f, "a 25-tonne hull coasts to rest in tens of metres, not hundreds");
    }

    [Fact]
    public void Pz_iv_hull_traverse_stays_in_a_tracked_vehicle_range()
    {
        var vehicle = GroundVehiclePhysicsFactory.Build(TankRoster.VehicleMediumB.Mobility);

        var state = Drive(
            GroundVehicleState.Rest(),
            vehicle,
            new GroundVehicleControls(1f, 1f),
            seconds: 10f);

        MathF.Abs(state.AngularVelocityRadPerSec).Should()
            .BeInRange(0.3f, 0.55f, "a medium tank should pivot deliberately, not rotate like a racing car");
    }

    [Fact]
    public void A_hard_powered_turn_scrubs_speed_instead_of_carving_at_full_cruise()
    {
        var vehicle = GroundVehiclePhysicsFactory.Build(TankRoster.VehicleMediumB.Mobility);
        var cruise = Drive(GroundVehicleState.Rest(), vehicle, throttle: 1f, seconds: 20f);

        var straight = Drive(cruise, vehicle, new GroundVehicleControls(1f, 0f), seconds: 5f);
        var turning = Drive(cruise, vehicle, new GroundVehicleControls(1f, 1f), seconds: 5f);

        turning.VelocityMps.Length().Should().BeLessThan(
            straight.VelocityMps.Length() * 0.95f,
            "tracks must spend their shared friction budget on the corner and scrub speed");
    }

    [Fact]
    public void Pz_iv_loses_ground_on_an_upgrade_versus_the_flat()
    {
        var vehicle = GroundVehiclePhysicsFactory.Build(TankRoster.VehicleMediumB.Mobility);
        // Enter the grade already at cruise (the historically-modest first-gear tractive effort makes
        // a from-rest climb marginal — see the gradeability note in the change summary). A ~4-degree
        // upgrade rising toward +X then robustly shows terrain feeding the dynamics: the same hull
        // covers markedly less ground in the same window because gravity-along-slope plus the reduced
        // (cos θ) contact grip bleed its speed.
        var uphill = Ground with { SurfaceHeightSampler = (x, _) => 0.07f * x };
        var cruise = Drive(GroundVehicleState.Rest(), vehicle, throttle: 1f, seconds: 20f);
        cruise.VelocityMps.Length().Should().BeGreaterThan(8f, "the tank must reach cruise before the grade");

        var flatRun = Drive(cruise, vehicle, throttle: 1f, seconds: 8f);
        var climbRun = Climb(cruise, vehicle, uphill, throttle: 1f, seconds: 8f);

        var flatDistance = Vector2.Distance(flatRun.PositionMeters, cruise.PositionMeters);
        var climbDistance = Vector2.Distance(climbRun.PositionMeters, cruise.PositionMeters);
        climbDistance.Should().BeGreaterThan(0f, "the medium tank still makes headway up a gentle grade");
        climbDistance.Should().BeLessThan(flatDistance * 0.85f, "but the upgrade bleeds its speed");
    }

    [Fact]
    public void Pz_iv_pulls_away_from_rest_up_a_real_grade_in_its_crawler_gear()
    {
        var vehicle = GroundVehiclePhysicsFactory.Build(TankRoster.VehicleMediumB.Mobility);
        // ~5-degree grade rising toward +X. The deeper first gear gives enough launch tractive
        // effort to beat grade pull + tracked rolling from a standstill — before the crawler-gear
        // tuning the medium tank could not even start up this slope (idle torque < resistance).
        var grade = Ground with { SurfaceHeightSampler = (x, _) => 0.0875f * x };

        var state = Climb(GroundVehicleState.Rest(), vehicle, grade, throttle: 1f, seconds: 20f);

        state.PositionMeters.X.Should().BeGreaterThan(
            5f, "the crawler gear lets the medium tank start and climb a 5-degree grade from rest");
    }

    private static GroundVehicleState Drive(GroundVehicleState state, GroundVehicleProperties vehicle, float throttle, float seconds)
        => Drive(state, vehicle, new GroundVehicleControls(throttle, 0f), seconds);

    private static GroundVehicleState Drive(
        GroundVehicleState state,
        GroundVehicleProperties vehicle,
        GroundVehicleControls controls,
        float seconds) => Run(state, vehicle, Ground, controls, seconds);

    private static GroundVehicleState Climb(
        GroundVehicleState state,
        GroundVehicleProperties vehicle,
        GroundVehicleEnvironment environment,
        float throttle,
        float seconds) => Run(state, vehicle, environment, new GroundVehicleControls(throttle, 0f), seconds);

    private static GroundVehicleState Run(
        GroundVehicleState state,
        GroundVehicleProperties vehicle,
        GroundVehicleEnvironment environment,
        GroundVehicleControls controls,
        float seconds)
    {
        for (var elapsed = 0f; elapsed < seconds; elapsed += Tick)
        {
            state = GroundVehicleIntegrator.Advance(state, vehicle, environment, controls, Tick);
        }

        return state;
    }
}
