using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Garupan.Content;

/// <summary>Ordered battle-map candidates loaded from content. Client resolution requires a
/// render model; authoritative-server resolution needs only simulation artefacts. Both require
/// the heightfield and any declared prop table, so a half-extracted city never activates.</summary>
public sealed class BattleMapCatalog
{
    internal BattleMapCatalog(IReadOnlyList<BattleMapSpec> maps) =>
        Maps = new ReadOnlyCollection<BattleMapSpec>(new List<BattleMapSpec>(maps));

    public IReadOnlyList<BattleMapSpec> Maps { get; }

    public int Count => Maps.Count;

    public BattleMapSpec? ResolveFirstRenderable(Func<string, bool> assetExists) =>
        ResolveFirstAvailable(assetExists, requireRenderModel: true);

    public BattleMapSpec? ResolveFirstAuthoritative(Func<string, bool> assetExists) =>
        ResolveFirstAvailable(assetExists, requireRenderModel: false);

    private BattleMapSpec? ResolveFirstAvailable(Func<string, bool> assetExists, bool requireRenderModel)
    {
        ArgumentNullException.ThrowIfNull(assetExists);
        foreach (var map in Maps)
        {
            if ((!requireRenderModel || assetExists(map.RenderModelFileName))
                && assetExists(map.HeightFieldFileName)
                && (map.PropsFileName is null || assetExists(map.PropsFileName))
                && (map.ObstaclesFileName is null || assetExists(map.ObstaclesFileName)))
            {
                return map;
            }
        }

        return null;
    }
}
