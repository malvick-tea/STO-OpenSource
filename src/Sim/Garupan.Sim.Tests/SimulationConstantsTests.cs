using FluentAssertions;
using Xunit;

namespace Garupan.Sim.Tests;

public sealed class SimulationConstantsTests
{
    [Fact]
    public void Fixed_timestep_seconds_is_the_reciprocal_of_ticks_per_second()
    {
        SimulationConstants.FixedTimestepSeconds.Should().BeApproximately(
            1f / SimulationConstants.TicksPerSecond,
            0.0000001f);
    }

    [Fact]
    public void Ticks_per_second_matches_the_concept_doc_target_of_60_hz()
    {
        SimulationConstants.TicksPerSecond.Should().Be(60);
    }

    [Fact]
    public void Max_frame_seconds_matches_the_fix_your_timestep_budget()
    {
        SimulationConstants.MaxFrameSeconds.Should().Be(
            0.250,
            "the 250 ms budget is the canonical value the C++ reference uses to avoid the spiral of death");
    }
}
