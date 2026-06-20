using System;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;

namespace Garupan.Sim.Terrain;

/// <summary>Pure CPU height + normal sampler over the bundled terrain heightfield — the GTHF blob
/// the map pipeline emits next to the terrain GLB, sharing its exact grid + extent. Maps a world
/// (X east, Z north) position to the surface height (Y up) by bilinear interpolation, and to the
/// surface normal by central differences, so both the deterministic ground-vehicle physics (slope
/// force, server-authoritative) and the match renderer (seating + tilting every tank onto the DEM
/// relief) read one source of truth instead of a flat plane.</summary>
/// <remarks>
/// Blob layout (little-endian): ASCII magic <c>GTHF</c>, int32 version, int32 grid size N,
/// float world size (metres, square), float reserved, float minY, float maxY, then N*N float32
/// heights row-major — row index runs +Z (north), column index runs +X (east), centred so the
/// map spans [-world/2, +world/2] on both axes about the origin.
/// </remarks>
public sealed class TerrainHeightField : IHeightSurface
{
    private const int HeaderBytes = 28;
    private const int MaxGridSize = 8192;

    private readonly float[] _heights;
    private readonly int _n;
    private readonly float _worldSize;
    private readonly float _half;
    private readonly float _cellSize;

    public TerrainHeightField(int gridSize, float worldSizeMeters, float[] heights)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(gridSize, 2);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(worldSizeMeters);
        ArgumentNullException.ThrowIfNull(heights);
        if (!float.IsFinite(worldSizeMeters)
            || gridSize > MaxGridSize
            || heights.LongLength != (long)gridSize * gridSize)
        {
            throw new ArgumentException("Height count must equal gridSize squared.", nameof(heights));
        }

        _n = gridSize;
        _worldSize = worldSizeMeters;
        _half = worldSizeMeters * 0.5f;
        _cellSize = worldSizeMeters / (gridSize - 1);
        _heights = heights;
    }

    public float WorldSizeMeters => _worldSize;

    /// <summary>Grid spacing in metres — the natural central-difference step for the surface normal.</summary>
    public float CellSizeMeters => _cellSize;

    public static TerrainHeightField Load(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderBytes
            || bytes[0] != (byte)'G' || bytes[1] != (byte)'T' || bytes[2] != (byte)'H' || bytes[3] != (byte)'F')
        {
            throw new InvalidDataException("Not a GTHF terrain heightfield blob.");
        }

        var version = BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]);
        if (version != 1)
        {
            throw new InvalidDataException($"Unsupported GTHF version {version}.");
        }

        var n = BinaryPrimitives.ReadInt32LittleEndian(bytes[8..]);
        var world = BinaryPrimitives.ReadSingleLittleEndian(bytes[12..]);
        var count = (long)n * n;
        var requiredBytes = HeaderBytes + (count * sizeof(float));
        if (n is < 2 or > MaxGridSize
            || !float.IsFinite(world)
            || world <= 0f
            || requiredBytes != bytes.Length)
        {
            throw new InvalidDataException("Corrupt GTHF header or truncated height data.");
        }

        var count32 = checked((int)count);
        var heights = new float[count32];
        var data = bytes.Slice(HeaderBytes, checked(count32 * sizeof(float)));
        for (var i = 0; i < count32; i++)
        {
            heights[i] = BinaryPrimitives.ReadSingleLittleEndian(data[(i * sizeof(float))..]);
            if (!float.IsFinite(heights[i]))
            {
                throw new InvalidDataException("GTHF contains a non-finite height value.");
            }
        }

        return new TerrainHeightField(n, world, heights);
    }

    /// <summary>Bilinearly samples the surface height at world (x east, z north). Positions
    /// off the map clamp to the nearest edge so a tank that drives past the terrain still has
    /// finite ground beneath it rather than falling through.</summary>
    public float HeightAt(float worldX, float worldZ)
    {
        var fx = (worldX + _half) / _worldSize * (_n - 1);
        var fz = (worldZ + _half) / _worldSize * (_n - 1);
        var x0 = Math.Clamp((int)MathF.Floor(fx), 0, _n - 1);
        var z0 = Math.Clamp((int)MathF.Floor(fz), 0, _n - 1);
        var x1 = Math.Min(x0 + 1, _n - 1);
        var z1 = Math.Min(z0 + 1, _n - 1);
        var tx = Math.Clamp(fx - x0, 0f, 1f);
        var tz = Math.Clamp(fz - z0, 0f, 1f);

        var top = Lerp(_heights[(z0 * _n) + x0], _heights[(z0 * _n) + x1], tx);
        var bottom = Lerp(_heights[(z1 * _n) + x0], _heights[(z1 * _n) + x1], tx);
        return Lerp(top, bottom, tz);
    }

    /// <summary>Upward surface normal at world (x east, z north), from a central-difference slope
    /// over one grid cell. Flat ground returns +Y; a slope returns a unit normal tilted away from
    /// the rise. Drives both the hull tilt (renderer) and the gravity-along-slope force (physics),
    /// so they always agree.</summary>
    public Vector3 NormalAt(float worldX, float worldZ) => NormalAt(worldX, worldZ, _cellSize);

    /// <summary>Surface normal sampled with an explicit central-difference step (metres). A larger
    /// step smooths small undulations to the footprint a vehicle of that scale actually bridges.</summary>
    public Vector3 NormalAt(float worldX, float worldZ, float sampleDistanceMeters)
    {
        return HeightSurfaceNormal.At(HeightAt, worldX, worldZ, sampleDistanceMeters);
    }

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
}
