using System;
using System.Collections.Generic;

namespace Garupan.Content;

/// <summary>
/// Loaded shell-visual catalog. Produced by <see cref="ShellVisualCsv.LoadFile"/> /
/// <see cref="ShellVisualCsv.Parse"/> from <c>data/shell-visuals.csv</c>. The renderer
/// queries via <see cref="Find"/> at frame time to pick the right mesh per projectile.
/// </summary>
/// <remarks>
/// <para>
/// Lookup returns <c>null</c> when an ammo family has no entry — the renderer falls
/// back to the procedural projectile cube. This keeps the asset roll-out gradual: as
/// new shells get authored, drop CSV rows; the demo + match screens upgrade
/// automatically. Until then they show cubes — visible but unmistakably placeholder.
/// </para>
/// </remarks>
public sealed class ShellVisualCatalog
{
    private readonly Dictionary<AmmoType, ShellVisualSpec> _byAmmo;

    internal ShellVisualCatalog(Dictionary<AmmoType, ShellVisualSpec> byAmmo)
    {
        ArgumentNullException.ThrowIfNull(byAmmo);
        _byAmmo = byAmmo;
    }

    /// <summary>Number of visual bindings in this catalog.</summary>
    public int Count => _byAmmo.Count;

    /// <summary>Returns the visual binding for the given ammo family, or null when no
    /// entry exists (the renderer falls back to the procedural cube).</summary>
    public ShellVisualSpec? Find(AmmoType ammo) =>
        _byAmmo.TryGetValue(ammo, out var found) ? found : null;

    /// <summary>Whether the catalog has a binding for the given ammo family.</summary>
    public bool Contains(AmmoType ammo) => _byAmmo.ContainsKey(ammo);

    public IEnumerable<ShellVisualSpec> All => _byAmmo.Values;

    /// <summary>An empty catalog — useful for tests / headless builds where no shell
    /// asset is available, so every projectile falls back to the procedural cube.</summary>
    public static ShellVisualCatalog Empty { get; } = new(new Dictionary<AmmoType, ShellVisualSpec>());
}
