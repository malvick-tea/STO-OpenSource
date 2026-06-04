using System;
using System.Collections.Generic;

namespace Garupan.Sim.Terrain;

/// <summary>A ground height surface = an optional base terrain plus the props felled this frame.
/// The match renderer rebuilds the member list each frame from the snapshot's felled set, then seats
/// every tank on the result so a hull rises over a fallen pole. Height only; tilt comes from sampling
/// this surface under the hull footprint (<see cref="FootprintSurfaceFit"/>).</summary>
public sealed class DynamicFelledPropSurface : IHeightSurface
{
    private readonly Func<float, float, float>? _baseHeightAt;
    private readonly List<FelledPropSurfaceMember> _members = new();

    public DynamicFelledPropSurface(Func<float, float, float>? baseHeightAt = null)
    {
        _baseHeightAt = baseHeightAt;
    }

    public bool HasMembers => _members.Count > 0;

    public void Clear() => _members.Clear();

    /// <summary>Adds a felled member; standing/shattered members are ignored (they add no height).</summary>
    public void Add(in FelledPropSurfaceMember member)
    {
        if (FelledPropSurface.IsContactable(member.State))
        {
            _members.Add(member);
        }
    }

    public float HeightAt(float worldX, float worldZ)
    {
        var propHeight = 0f;
        for (var i = 0; i < _members.Count; i++)
        {
            propHeight = MathF.Max(propHeight, FelledPropSurface.HeightContribution(_members[i], worldX, worldZ));
        }

        return (_baseHeightAt?.Invoke(worldX, worldZ) ?? 0f) + propHeight;
    }
}
