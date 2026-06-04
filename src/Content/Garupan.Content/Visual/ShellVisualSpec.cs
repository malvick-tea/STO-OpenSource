namespace Garupan.Content;

/// <summary>
/// Visual binding for one <see cref="AmmoType"/> — points at the 3D model the renderer
/// should draw while the round is in flight. Authoring data per ADR-0030: artists drop
/// a glTF asset in <c>content/shell/&lt;gun&gt;/&lt;round&gt;/</c> and add a row to
/// <c>data/shell-visuals.csv</c>; no C# edits required.
/// </summary>
/// <param name="AmmoType">Which ammunition family this visual applies to.</param>
/// <param name="ModelVfsPath">Virtual path to the model. Supports both
/// <c>res://*.glb</c> (binary glTF) and <c>res://*.gltf</c> (Sketchfab-style split
/// gltf+bin sidecar) — the loader picks the right path automatically.</param>
/// <remarks>
/// Phase-0 keying is by <see cref="AmmoType"/> alone: every AP round renders with the
/// catalog's AP entry regardless of caliber. When per-caliber shell models arrive (e.g.
/// a 75mm AP round for the medium tank vs the existing 88mm one for the heavy tank), the spec
/// grows a <c>CaliberId</c> field and the catalog re-keys; the CSV gets one column
/// extra. For now the alpha-bound demo shows one shell shape across the AP roster.
/// </remarks>
public sealed record ShellVisualSpec(
    AmmoType AmmoType,
    string ModelVfsPath);
