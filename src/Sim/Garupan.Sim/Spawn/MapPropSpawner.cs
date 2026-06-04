using System;
using System.Collections.Generic;
using Garupan.Content;
using Garupan.Sim.Components;
using Opus.Engine.Physics.Destruction;

namespace Garupan.Sim.Spawn;

/// <summary>
/// Turns a map's <see cref="MapProp"/> rows into destructible-prop entities. Each prop's two
/// failure thresholds and its mass are derived once, here, from its physical size and the
/// material its <see cref="PropKind"/> maps to (<see cref="PropKindCatalog"/>) — so the runtime
/// collision system never recomputes physics, and adding props to a map is pure data.
/// </summary>
public static class MapPropSpawner
{
    /// <summary>Height (m) at which a tank hull bears on an upright member. A tank pushes a tall
    /// trunk near hull height and a short post near its top, so the lever the resisting moment
    /// acts through is the smaller of this and the prop's own height. It is a tank-hull geometry
    /// figure, not a tuning knob.</summary>
    private const float NominalHullContactHeightMeters = 1.0f;

    public static void Spawn(World world, IEnumerable<MapProp> props)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(props);

        // The enumeration index is the prop's stable wire id: the client loads the same CSV in the
        // same order, so id == row index links a felled prop's replicated state to the box it draws.
        var propId = 0;
        foreach (var prop in props)
        {
            SpawnOne(world, prop, propId);
            propId++;
        }
    }

    public static EntityHandle SpawnOne(World world, MapProp prop, int propId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(prop);
        var archetype = PropKindCatalog.For(prop.Kind);
        var material = archetype.Material;

        // Rupture moment M_max = σ·Z is the shared root of both thresholds: the topple ENERGY is
        // M_max carried through the failure deflection; the resisting FORCE is M_max over the lever
        // the tank pushes through. One physical quantity, two ways the hull can defeat it.
        var ruptureMoment = material.ModulusOfRupturePa * PropResistance.CircularSectionModulus(prop.BaseDiameterMeters);
        var leverArmMeters = MathF.Min(NominalHullContactHeightMeters, prop.HeightMeters);

        var component = new DestructibleProp
        {
            PropId = propId,
            Kind = prop.Kind,
            Behavior = archetype.Behavior,
            State = PropState.Standing,
            ToppleEnergyJoules = ruptureMoment * material.FailureDeflectionRadians,
            ResistingForceNewtons = leverArmMeters > 0f ? ruptureMoment / leverArmMeters : float.PositiveInfinity,
            RadiusMeters = prop.BaseDiameterMeters * 0.5f,
            MassKg = material.DensityKgPerCubicMeter * CylinderVolumeCubicMeters(prop.BaseDiameterMeters, prop.HeightMeters),
            FallYawRadians = prop.YawRadians,
            StateSeconds = 0f,
        };

        return world.Spawn(new Transform(prop.GroundPosition, prop.YawRadians), component);
    }

    /// <summary>Volume of the standing member approximated as a solid cylinder of the base
    /// diameter and the prop height — enough to weigh it for the block response and debris.</summary>
    private static float CylinderVolumeCubicMeters(float diameterMeters, float heightMeters)
    {
        var radius = diameterMeters * 0.5f;
        return MathF.PI * radius * radius * heightMeters;
    }
}
