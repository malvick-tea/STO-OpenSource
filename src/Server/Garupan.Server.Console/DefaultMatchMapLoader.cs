using System;
using System.IO;
using Garupan.Content;
using Garupan.Sim.Terrain;

namespace Garupan.Server.Console;

/// <summary>Loads authoritative battle-map artefacts from the shared ordered map catalog.
/// Optional: an asset-light build without the catalog or any complete map yields flat ground.</summary>
public static class DefaultMatchMapLoader
{
    private const string ContentDirectoryName = "content";
    private const string MapDirectoryName = "maps";
    private const string CatalogFileName = "catalog.csv";

    /// <summary>Resolves the first server-ready map: heightfield plus the prop table when one is
    /// declared. The GPU model is deliberately not loaded or bundled by the headless server.</summary>
    public static DefaultMatchMap? TryLoad(string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(baseDirectory);
        var catalogPath = ResolveAssetPath(baseDirectory, CatalogFileName);
        if (!File.Exists(catalogPath))
        {
            return null;
        }

        var catalog = BattleMapCsv.LoadFile(catalogPath);
        var map = catalog.ResolveFirstAuthoritative(
            fileName => File.Exists(ResolveAssetPath(baseDirectory, fileName)));
        if (map is null)
        {
            return null;
        }

        var heightField = TerrainHeightField.Load(File.ReadAllBytes(ResolveAssetPath(baseDirectory, map.HeightFieldFileName)));
        var props = map.PropsFileName is null
            ? Array.Empty<MapProp>()
            : MapPropCsv.LoadFile(ResolveAssetPath(baseDirectory, map.PropsFileName));
        var obstacles = map.ObstaclesFileName is null
            ? Array.Empty<MapObstacle>()
            : MapObstacleCsv.LoadFile(ResolveAssetPath(baseDirectory, map.ObstaclesFileName));
        return new DefaultMatchMap(map.Id, heightField.HeightAt, props, obstacles);
    }

    private static string ResolveAssetPath(string baseDirectory, string fileName) =>
        Path.Combine(baseDirectory, ContentDirectoryName, MapDirectoryName, fileName);
}
