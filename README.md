# STO-openSource

STO-openSource is the game-side codebase for a client/server vehicle-combat
project built on the Opus engine. It contains the application code that turns
engine services into a match: content catalogs, simulation, network protocol,
authoritative server logic, UI screens, a Windows D3D12 client host, and developer
tools.

## Project State

This is not a finished game. It is a source snapshot from the period when STO was
approaching its first alpha. Some features are incomplete, some paths are rough,
and bugs should be expected. Treat the code as a working foundation for study,
experimentation, and continued development rather than a complete game.

See [`CONTRIBUTORS.md`](CONTRIBUTORS.md) for a summary of contributor work.

The repository is code-first. Runtime content is expected to come from a local
content workspace or from a host package. That separation is deliberate: the code
defines how content is parsed, validated, spawned, simulated, rendered, and
replicated, while the concrete scenario data can be swapped without changing the
core systems.

The project names and namespaces currently use `Garupan.*`. Treat those names as
stable technical identifiers inside this tree.

## What The Code Does

At a high level, STO is made of five cooperating parts:

1. A deterministic simulation layer that owns entities, components, systems,
   fixed-step ticking, snapshots, and replays.
2. A content layer that turns catalog rows into strongly typed specs for match
   modes, vehicles, weapons, crews, map props, visuals, and audio profiles.
3. An authoritative server layer that accepts players, seats them in a match,
   receives input frames, advances the simulation, evaluates outcomes, and sends
   snapshots.
4. A client layer that owns boot flow, settings, progress state, menus, match UI,
   command-map UI, and D3D12 presentation.
5. A tooling layer that checks local content and localisation inputs before they
   are used by the client or server.

The Opus engine supplies platform services, rendering, UI drawing primitives,
network transport contracts, persistence, localisation, and content primitives.
STO composes those services into game behavior.

## Runtime Shape

The normal match path looks like this:

```text
local content
  -> Garupan.Content parsers and validators
  -> MatchHostOptionsFactory
  -> MatchHost
  -> Garupan.Sim fixed-step pipeline
  -> WorldSnapshot / match frames
  -> client UI and D3D12 presentation
```

The server is authoritative. Clients send input frames; the server applies those
inputs during fixed ticks and sends snapshots back. Client code is allowed to
present, interpolate, capture input, and display UI state, but match outcomes
belong to the server.

## Repository Layout

Only the main areas are listed here. For a deeper map, read
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

```text
src/Client       client state, UI, boot stages, Windows D3D12 host, demo harness
src/Content      catalog specs, CSV parsers, validation, map and vehicle data types
src/Localisation localisation key surface
src/Net          session-level wire messages
src/Server       authoritative match host and console runner
src/Sim          ECS world, fixed-step systems, snapshots, replay, protocol
src/Tools        content lint and localisation lint commands
```

## Simulation

`Garupan.Sim` is the core gameplay model. It wraps an Arch ECS world behind a
small local API and runs behavior through ordered systems.

Important concepts:

- `World` is the stable wrapper around the ECS storage.
- Components are plain state.
- Systems are fixed-step behavior.
- `SystemPipeline` executes systems in order.
- `MatchPipelineFactory` builds the canonical match system list.
- `FixedStepLoop` converts variable frame time into deterministic ticks.
- `SnapshotCapture`, encoders, and decoders move world state across the network.
- Replay types record deterministic input/state sequences for later inspection.

System order is meaningful. Input is applied early, movement and collision happen
before aiming and firing, projectile hits resolve after projectile integration,
and cleanup runs late. When adding behavior, place it in the correct band rather
than relying on incidental order.

## Server

`Garupan.Server.Match` owns match authority. It composes:

- an `INetTransport`;
- a `ServerSession`;
- a `Garupan.Sim.World`;
- a fixed-step simulation pipeline;
- a roster;
- spawn planning;
- outcome tracking;
- snapshot broadcasting.

`MatchHost.Pump` is the central driver. It drains transport events, advances the
fixed-step loop, evaluates the match, and broadcasts snapshots at the configured
interval.

`Garupan.Server.Console` is the process boundary. It parses command-line options,
configures logging, creates the host bundle, and runs the tick loop. Keep process
concerns there; keep match rules in `Garupan.Server.Match`.

## Client

The client side is split by responsibility:

- `Garupan.Client.Core` handles settings, progress state, boot sequencing, and
  service registration.
- `Garupan.Client.Ui` contains screens, navigation, HUD rendering, command-map
  state, settings UI, lobby UI, and match UI.
- `Garupan.Client.Windows.Bootstrap` builds the boot stage sequence and logging.
- `Garupan.Client.Windows.D3D12` binds Opus D3D12 services to the client.
- `Garupan.Garage.Demo` is a focused demo harness for scene and simulation
  presentation code.

The UI layer uses engine draw-surface abstractions. D3D12-specific resource
ownership stays in the Windows D3D12 project.

## Content

`Garupan.Content` describes the data the simulation and server consume. It does
not decide match behavior by itself. Its job is to parse, validate, and expose
structured specs.

Common catalog families:

- match modes and match compositions;
- spawn layouts;
- vehicle specs;
- gun, mount, ammo, and penetration specs;
- armor and mobility specs;
- crew rosters;
- bot personalities;
- map props, obstacles, materials, and battle map metadata;
- shell visuals and audio profiles.

Good content code produces clear parse errors and keeps IDs stable. The server
and simulation should receive already validated specs.

## Tools

`Garupan.Tools` is a small command dispatcher. It currently contains:

- `content-lint` for checking local content catalogs;
- `loc-lint` for checking localisation keys and catalogs.

Tool code should keep IO at the command boundary. Scanners, parsers, and reports
should be easy to test with in-memory text.

## Dependency On Opus

Place `OpusOpenSource` next to `STO-openSource`:

```text
Documents/
  OpusOpenSource/
  STO-openSource/
```

STO projects reference Opus projects through sibling project references. If you
move either folder, update those references or keep the same relative layout.

## Requirements

- .NET SDK 8.
- Windows for D3D12 client projects.
- A sibling `OpusOpenSource` folder.

## Build

From `STO-openSource`:

```powershell
dotnet restore .\Garupan.sln
dotnet build .\Garupan.sln
```

Build output goes under `build/output/`.

## Test

Run one focused project first:

```powershell
dotnet test .\src\Sim\Garupan.Sim.Tests\Garupan.Sim.Tests.csproj
```

Run the whole solution when your local content and machine setup match the tests
you want to exercise:

```powershell
dotnet test .\Garupan.sln
```

Some tests build all inputs in memory. Others intentionally exercise file-backed
runtime paths. Treat missing local content as an environment issue unless the
test itself is meant to construct that content.

## Run The Server

Show help:

```powershell
dotnet run --project .\src\Server\Garupan.Server.Console\Garupan.Server.Console.csproj -- --help
```

Start a local server:

```powershell
dotnet run --project .\src\Server\Garupan.Server.Console\Garupan.Server.Console.csproj -- --bind 127.0.0.1 --port 7777
```

Useful options:

```text
--port N
--bind ADDRESS
--mode ID
--tick-hz N
--snapshot-interval N
--frame-pump-hz N
--no-file-log
```

## Workflows

### Add Simulation Behavior

1. Add state as a component.
2. Add behavior as a system.
3. Register the system through `MatchPipelineFactory`.
4. Update spawners if the component is created from content specs.
5. Update snapshots or replay only if the state crosses those boundaries.
6. Add tests around ordering and determinism.

### Add A Content Catalog

1. Define a spec type.
2. Add a parser near the related catalog family.
3. Add validation rules.
4. Test the parser with small in-memory input.
5. Wire the validated spec into server or simulation code.
6. Add tool coverage if the catalog participates in linting.

### Add A Server Rule

1. Put match rules in `Garupan.Server.Match`.
2. Keep command-line parsing in `Garupan.Server.Console`.
3. Test option conversion separately from match behavior.
4. Keep network frame projection small and explicit.

### Add Client UI

1. Put shared state in `Client.Core`.
2. Put screen state and rendering in `Client.Ui`.
3. Use engine draw-surface primitives.
4. Move to `Client.Windows.D3D12` only for platform or graphics resources.
5. Add tests for pure layout and state transitions.

## Reading Order

If you are new to the codebase, read in this order:

1. `src/Sim/Garupan.Sim/Ecs/World.cs`
2. `src/Sim/Garupan.Sim/Loop/MatchPipelineFactory.cs`
3. `src/Server/Garupan.Server.Match/MatchHost.cs`
4. `src/Server/Garupan.Server.Match/MatchHostOptionsFactory.cs`
5. `src/Content/Garupan.Content/Catalogs/CatalogValidator.cs`
6. `src/Client/Garupan.Client.Ui/Navigation/ScreenStack.cs`
7. `src/Client/Garupan.Client.Windows.D3D12/D3D12HostBundle.cs`
8. `src/Tools/Garupan.Tools/Cli/CommandDispatcher.cs`

## Rules Of Thumb

- Simulation should not know about rendering.
- Server authority should not depend on client UI.
- Content parsers should return structured data and clear errors.
- Client presentation should tolerate missing optional local content with clear
  diagnostics.
- Tests should prefer small constructed inputs over large fixture trees.
- New identifiers for sample data should be neutral and descriptive.
