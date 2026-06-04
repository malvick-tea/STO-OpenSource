using System;
using System.Numerics;

namespace Garupan.Sim.Terrain;

/// <summary>Central-difference normals for any height sampler.</summary>
public static class HeightSurfaceNormal
{
    public static Vector3 At(
        Func<float, float, float> heightAt,
        float worldX,
        float worldZ,
        float sampleDistanceMeters)
    {
        ArgumentNullException.ThrowIfNull(heightAt);
        var d = MathF.Max(sampleDistanceMeters, 1e-3f);
        var slopeX = (heightAt(worldX + d, worldZ) - heightAt(worldX - d, worldZ)) / (2f * d);
        var slopeZ = (heightAt(worldX, worldZ + d) - heightAt(worldX, worldZ - d)) / (2f * d);
        return Vector3.Normalize(new Vector3(-slopeX, 1f, -slopeZ));
    }
}
