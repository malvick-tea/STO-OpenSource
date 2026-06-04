# Contributors

This file summarizes the main contribution areas reflected in the project commit
history. It is a practical attribution note, not a complete changelog.

## idktea

idktea led most of the post-engine-sync game work and carried the project from a
2D/network prototype toward the first-alpha STO shape.

Main contribution areas:

- Reworked the game repository to consume the Opus engine as a submodule and
  kept the game/engine project boundary buildable after the split.
- Rewrote project documentation around the Opus D3D12 engine, submodule layout,
  build flow, testing, and layer structure.
- Built the 3D network match presentation path: scene planning, snapshot-to-scene
  projection, chase/orbit camera behavior, D3D12 match-scene rendering, ground
  plane composition, sky/backdrop handling, projectile placement, and live GPU
  smoke coverage.
- Added and refined match-scene effects and presentation details, including
  projectile orientation, shell presentation, articulated vehicle scene controls,
  muzzle effects, loading progress, and clean leave/stop behavior for match audio.
- Moved major gameplay data from hardcoded C# constants into catalog-driven
  content paths, including vehicle rosters, weapon mounting, drive envelopes,
  audio profiles, map data, prop kinds, visual profiles, and validation tests.
- Expanded the simulation model with force-based ground movement, exterior
  ballistics, recoil, layered armor, penetration curves, terrain coupling,
  slope-aware seating, destructible props, static obstacles, and deterministic
  regression coverage.
- Built and iterated map-generation tooling for large local battle spaces,
  material manifests, PBR texture workflows, heightfields, obstacles, and prop
  tables.
- Wired data-driven audio behavior for vehicle movement, firing, reload, turret
  movement, pivoting, and fade-in/fade-out loop handling.
- Fixed first-alpha playtest issues: server bind messaging, input sensitivity,
  scene loading stalls, audio continuing after screen exit, missing scene
  collision, visual ghosting of props, and local presentation rough edges.
- Kept build and test coverage moving with focused tests across content, sim,
  server, client UI, D3D12, and generator contracts.

## VellumYu

VellumYu handled the major engine synchronization and repository migration work
that made the game build against the current Opus engine line.

Main contribution areas:

- Established the baseline before the Opus engine synchronization.
- Vendored the current Opus engine source into the game tree for the migration
  pass and renamed engine assemblies/namespaces to the `Opus.*` identity.
- Repointed game projects from the old embedded engine shape to the Opus engine
  assemblies while preserving the game-side `Garupan.*` project identity.
- Rebuilt solution/project metadata, central build properties, identity
  overrides, package versioning, style configuration, and project references.
- Restored game-side content and localisation projects after engine extraction,
  including the catalog/spec surface and localisation key registry.
- Drove the combined solution through strict build rules, StyleCop cleanup,
  formatter passes, warning cleanup, and API-shape adaptation.
- Restored content test coverage for catalog and parser behavior after the
  engine/game split.
- Synced engine documentation into the game documentation area during the
  migration and wrote the final migration report for that phase.

## Documentation Note

The project documentation was prepared with the assistance of Claude AI
(model: opus 4.8).
