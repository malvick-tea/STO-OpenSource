namespace Garupan.Client.Windows.Direct3D12.Composition.Models;

/// <summary>VFS roots for battle-map content. Concrete map file names come from
/// <c>content/maps/catalog.csv</c>; code owns only the mounted content location.</summary>
internal static class BattleMapVfsPaths
{
    private const string MapDirectoryPath = "res://maps/";

    public const string CatalogPath = $"{MapDirectoryPath}catalog.csv";

    public static string AssetPath(string fileName) => $"{MapDirectoryPath}{fileName}";
}
