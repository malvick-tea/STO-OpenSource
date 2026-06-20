# Architecture

This document maps the STO codebase by runtime responsibility. It is not a file
index. The goal is to explain how the parts cooperate when a match starts,
ticks, replicates, and shuts down.

## Layer Model

```text
Client UI and presentation
  -> client services and input
  -> session protocol
  -> authoritative server
  -> fixed-step simulation
  -> content specs
  -> Opus engine services
```

The important boundary is authority:

- The server owns match truth.
- The simulation owns deterministic state updates.
- Content owns declarative inputs.
- The client owns presentation and local interaction.
- Opus owns engine services.

## Project Groups

### Client Core

Project:

```text
src/Client/Garupan.Client.Core
```

Responsibilities:

- user settings;
- multiplayer endpoint settings;
- progress state;
- boot sequencing;
- service registration;
- exit service abstraction;
- persistence framing through Opus persistence services.

This project is not a renderer. It should stay useful in tests and alternate
hosts.

### Client UI

Project:

```text
src/Client/Garupan.Client.Ui
```

Responsibilities:

- screen stack and transitions;
- main menu, settings, lobby, match, result, and campaign-style screens;
- command-map state, tools, strokes, tokens, input translation, and rendering;
- HUD readouts and reticle rendering;
- network match input capture and camera control.

The UI renders through `IDrawSurface` and model renderer interfaces. It should
not allocate D3D12 resources directly.

### Windows Client Host

Projects:

```text
src/Client/Garupan.Client.Windows.Bootstrap
src/Client/Garupan.Client.Windows.D3D12
```

Responsibilities:

- boot stage order;
- logging setup;
- window, input, draw surface, font atlas, scene viewport, and D3D12 host bundle;
- D3D12 model loading and model rendering;
- scene-specific presentation helpers such as shot effects and motion tracking.

This layer adapts Opus platform and renderer services to the STO client.

### Demo Harness

Project:

```text
src/Client/Garupan.Garage.Demo
```

Responsibilities:

- small local scene harness;
- simulation-to-world transform mapping;
- pause and match lifecycle state;
- shell casing presentation state;
- input bindings for the demo.

This project is useful for testing presentation code without bringing up the
full client flow.

### Content

Project:

```text
src/Content/Garupan.Content
```

Responsibilities:

- parse catalog input;
- validate references between catalog families;
- define specs consumed by simulation, client, and server;
- keep scenario data strongly typed before it crosses into runtime systems.

Important families:

- `Catalogs/`
- `Maps/`
- `Specs/Vehicle/`
- `Specs/Crew/`
- `Specs/Narrative/`
- `Visual/`
- `Audio/`

### Localisation

Project:

```text
src/Localisation/Garupan.Localisation
```

Responsibilities:

- shared key definitions;
- stable key surface for UI and tools.

The actual catalog loading primitives come from Opus localisation.

### Session Network

Project:

```text
src/Net/Garupan.Net.Session
```

Responsibilities:

- session-level messages;
- wire helpers used above the raw transport.

Transport contracts and UDP implementation are in Opus.

The game relies on Opus UDP protocol v2 for an authenticated challenge-response
handshake, per-session HMAC keys, monotonic frame sequences, replay rejection,
source-address rate limiting, and bounded peer tables. Game-layer codecs still
validate sizes, finite values, enum domains, ownership, tick windows, and input
rate before mutating authoritative state.

### Match Server

Project:

```text
src/Server/Garupan.Server.Match
```

Responsibilities:

- connected player roster;
- team assignment;
- match options;
- spawn planning;
- authoritative host loop;
- input routing;
- snapshot projection;
- per-peer snapshot visibility filtering;
- match outcome tracking;
- match reset.

The central type is `MatchHost`.

### Server Console

Project:

```text
src/Server/Garupan.Server.Console
```

Responsibilities:

- process entry point;
- command-line parsing;
- server host bundle creation;
- tick-loop pumping;
- shutdown signal handling;
- console and file logging setup.

This layer should stay thin. Match policy belongs in `Server.Match`.

### Simulation

Project:

```text
src/Sim/Garupan.Sim
```

Responsibilities:

- ECS world wrapper;
- components;
- systems;
- fixed-step pipeline;
- spawn builders;
- collision and terrain helpers;
- deterministic seed handling;
- snapshot codec;
- replay codec;
- replay HMAC verification;
- compact protocol frames.

The simulation layer does not depend on client rendering.

### Tools

Project:

```text
src/Tools/Garupan.Tools
```

Responsibilities:

- command dispatch;
- content linting;
- localisation linting;
- reports and exit codes.

Tools should keep scanners and validators independent from console IO when
possible.

## Match Startup

```text
ServerConsoleOptionsParser
  -> ServerHostBundle
  -> MatchHostOptionsFactory
  -> MatchHost
  -> World.Create
  -> MapObstacleSpawner / MapPropSpawner
  -> MatchPipelineFactory.Build
```

At startup, the server resolves options, creates the authoritative world, spawns
static map entities, builds the system pipeline, and subscribes to session
events.

## Peer Lifecycle

```text
Connected
  -> allocate spawn slot
  -> assign network id
  -> spawn controlled entity
  -> send welcome frame

Received input
  -> decode owner
  -> find connected player
  -> write PendingInput
  -> ApplyInputsSystem consumes on next tick

Disconnected
  -> destroy entity
  -> remove roster entry
```

The server never trusts the client with final state. Client input is only a
request that becomes part of the next authoritative tick.

## Tick Lifecycle

```text
MatchHost.Pump(delta)
  -> ServerSession.Pump()
  -> FixedStepLoop.Pump(delta)
  -> MatchHost.OnTick(time)
  -> SystemPipeline.Tick(world, time, seed)
  -> MatchOutcomeTracker
  -> SnapshotCapture
  -> broadcast frames
```

When the match is decided, the host freezes simulation ticks for the match and
uses the configured hold/reset behavior.

## Simulation Pipeline

The default pipeline currently contains:

```text
ApplyInputsSystem
AiBotSystem
HullDriveSystem
ObstacleCollisionSystem
PropCollisionSystem
TurretAimSystem
GunRecoilTickSystem
ReloadTickSystem
ProjectileIntegrateSystem
GunFireSystem
ProjectileHitResolveSystem
RespawnSystem
SpawnInvulnerabilitySystem
LifetimeDecaySystem
CleanupDeadSystem
```

The order is part of the behavior. For example, fire resolution should not move
before input application, and cleanup should not happen before systems that need
to inspect the current tick's results.

## Snapshot And Replay Boundaries

Snapshots are for network replication. Replays are for deterministic recording
and inspection. Do not put every component into both by default.

Add state to a boundary only when:

- the client must display it;
- the server must send it;
- a replay must reproduce it;
- a test needs to assert it through the public contract.

## Content Boundary

Content specs are declarative. Runtime behavior belongs in simulation or server
code. A content row may describe a weapon, vehicle, prop, spawn, or mode; it
should not contain hidden logic that only one caller understands.

Validation should happen before match creation. The match host should receive a
coherent set of options and specs.

## Client Boundary

Client UI consumes snapshots and local state. It can:

- show menus;
- render HUD;
- collect input;
- draw command-map marks;
- present scenes;
- show match outcome state.

It should not:

- decide authoritative match results;
- mutate server state directly;
- put D3D12 resources in shared UI code;
- parse large content sets during a screen render.

## Tools Boundary

Tools are allowed to read local files and print reports. The parsing and scanning
logic they call should remain testable without console IO.

When adding a tool command, write it as:

```text
ICommand
  -> options parser
  -> scanner/validator
  -> report
  -> exit code
```

## Test Architecture

Test projects follow the same boundaries:

- simulation tests cover deterministic state and systems;
- content tests cover parsing and validation;
- server tests cover host behavior and options;
- client UI tests cover state, layout, and draw commands;
- D3D12 tests cover host and renderer integration;
- tool tests cover command parsing, scanning, reports, and exit codes.

Prefer focused tests that build their inputs directly. Use file-backed tests only
when path handling or local content loading is the behavior under test.
