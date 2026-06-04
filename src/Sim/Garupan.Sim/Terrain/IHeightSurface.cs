namespace Garupan.Sim.Terrain;

/// <summary>World-space ground height queried by match rendering (hull seating) and, on the DEM
/// maps, the deterministic ground-vehicle slope physics. Height only — orientation is derived from
/// the surface under a vehicle's whole footprint (<see cref="FootprintSurfaceFit"/>), not a point
/// normal, so a rigid hull bridges sub-footprint bumps instead of conforming its deck to them.</summary>
public interface IHeightSurface
{
    float HeightAt(float worldX, float worldZ);
}
