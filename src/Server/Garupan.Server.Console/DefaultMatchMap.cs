using System;
using System.Collections.Generic;
using Garupan.Content;

namespace Garupan.Server.Console;

/// <summary>Authoritative simulation assets for the first complete battle-map candidate.</summary>
public sealed record DefaultMatchMap(
    string Id,
    Func<float, float, float> TerrainHeightSampler,
    IReadOnlyList<MapProp> Props,
    IReadOnlyList<MapObstacle> Obstacles);
