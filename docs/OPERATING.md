# Operating Guide

This guide covers daily work with STO-openSource: building, testing, running the
server, using tools, and making changes without crossing layer boundaries.

## Local Layout

Use this sibling layout:

```text
Documents/
  OpusOpenSource/
  STO-openSource/
```

STO projects reference Opus projects through relative paths. If the folders move,
update the `ProjectReference` entries or keep the same relative placement.

## Requirements

- .NET SDK 8.
- Windows for D3D12 client projects.
- A shell that can run `dotnet`.
- Optional local content workspace for file-backed catalog, map, media, and
  localisation checks.

## First Build

```powershell
cd C:\Users\DAS\Documents\STO-openSource
dotnet restore .\Garupan.sln
dotnet build .\Garupan.sln
```

Expected result: the solution compiles and writes generated files under
`build/output/`.

To clean:

```powershell
Remove-Item -Recurse -Force .\build
```

## Build One Area

Simulation:

```powershell
dotnet build .\src\Sim\Garupan.Sim\Garupan.Sim.csproj
```

Server match logic:

```powershell
dotnet build .\src\Server\Garupan.Server.Match\Garupan.Server.Match.csproj
```

Client UI:

```powershell
dotnet build .\src\Client\Garupan.Client.Ui\Garupan.Client.Ui.csproj
```

Tools:

```powershell
dotnet build .\src\Tools\Garupan.Tools\Garupan.Tools.csproj
```

D3D12 client:

```powershell
dotnet build .\src\Client\Garupan.Client.Windows.D3D12\Garupan.Client.Windows.D3D12.csproj
```

## Test Strategy

Start with the project closest to your change. Then run one neighboring layer.

Examples:

```powershell
dotnet test .\src\Sim\Garupan.Sim.Tests\Garupan.Sim.Tests.csproj
dotnet test .\src\Server\Garupan.Server.Match.Tests\Garupan.Server.Match.Tests.csproj
dotnet test .\src\Content\Garupan.Content.Tests\Garupan.Content.Tests.csproj
dotnet test .\src\Tools\Garupan.Tools.Tests\Garupan.Tools.Tests.csproj
dotnet test .\src\Client\Garupan.Client.Ui.Tests\Garupan.Client.Ui.Tests.csproj
```

Run the full solution when you want a broad check:

```powershell
dotnet test .\Garupan.sln
```

Some tests exercise local file-backed paths. If a failure is about missing local
runtime content, decide whether the test needs a local fixture or whether you
should run a narrower pure-code test first.

## Running The Server

Show help:

```powershell
dotnet run --project .\src\Server\Garupan.Server.Console\Garupan.Server.Console.csproj -- --help
```

Run on loopback:

```powershell
dotnet run --project .\src\Server\Garupan.Server.Console\Garupan.Server.Console.csproj -- --bind 127.0.0.1 --port 7777
```

Use an OS-assigned port:

```powershell
dotnet run --project .\src\Server\Garupan.Server.Console\Garupan.Server.Console.csproj -- --port 0
```

Use a slower local tick rate:

```powershell
dotnet run --project .\src\Server\Garupan.Server.Console\Garupan.Server.Console.csproj -- --tick-hz 30 --frame-pump-hz 60 --auth-key-file C:\keys\sto-session.key
```

Keep `--frame-pump-hz` greater than or equal to `--tick-hz`.

## Server Options

The parser accepts:

```text
--port N
--bind ADDRESS
--mode ID
--tick-hz N
--snapshot-interval N
--frame-pump-hz N
--auth-key-file PATH
--allowlist-file PATH
--admin-token-file PATH
--max-players N
--public
--log-level LEVEL
--no-file-log
--help
```

Rules:

- `--port` is `0..65535`.
- `--bind` must parse as an IP address.
- `--tick-hz` must be positive.
- `--snapshot-interval` must be positive.
- `--frame-pump-hz` must not be lower than `--tick-hz`.
- `--mode` is a content ID resolved later by match option creation.
- `--auth-key-file` is required before the host opens a network socket.
- `--allowlist-file` optionally restricts peers to listed IPv4 source addresses.
- `--admin-token-file` enables `kick <network-id> <token>` commands on local stdin.
- `--max-players` must be positive and is also capped by the selected mode.
- a non-loopback `--bind` requires an explicit `--public` acknowledgement.
- `--log-level` defaults to `information`.

The client reads the same authenticated-session key from
`STO_AUTH_KEY_FILE`. Do not commit runtime keys. Use separate keys per
environment and rotate a key if it is disclosed.

Parser code should stay side-effect free. It returns parsed options, help, or a
diagnostic.

## Running Tools

Show dispatcher help:

```powershell
dotnet run --project .\src\Tools\Garupan.Tools\Garupan.Tools.csproj -- --help
```

Run a command:

```powershell
dotnet run --project .\src\Tools\Garupan.Tools\Garupan.Tools.csproj -- <command> <options>
```

Current command families:

- content lint;
- localisation lint.

When a tool reports missing input, check the path relative to the current shell
folder. Prefer absolute paths when debugging command behavior.

## Working With Local Content

Keep local runtime content in a separate workspace or a local ignored folder.
Recommended local shape:

```text
STO-openSource/
  .local/
    content/
    logs/
    reports/
```

Do not make runtime content part of code changes unless the content is a tiny
neutral fixture used by a test.

Content workflow:

1. Add or edit local catalog input.
2. Run the related parser test if one exists.
3. Run content lint.
4. Start the server with the mode that uses the content.
5. Add a focused regression test for parser or validation behavior.

## Simulation Workflow

Use this workflow for fixed-step behavior:

1. Identify the state that must change.
2. Add or update a component in `Garupan.Sim.Components`.
3. Add or update a system in `Garupan.Sim.Systems`.
4. Place the system in the correct order through `MatchPipelineFactory`.
5. Update spawn conversion only if the component is created from content specs.
6. Update snapshot or replay code only when the new state must cross that
   boundary.
7. Add tests for the behavior and for edge cases.

Avoid mixing responsibilities:

- A system should not parse catalog files.
- A spawner should not advance time.
- A protocol codec should not decide gameplay.
- Client UI should not mutate authoritative state.

## Server Workflow

Use this workflow for match rules:

1. Put match behavior in `Garupan.Server.Match`.
2. Keep console parsing in `Garupan.Server.Console`.
3. If the behavior depends on content, pass it through `MatchHostOptions`.
4. Test option creation separately from `MatchHost`.
5. Test match host behavior with loopback or in-memory transport where possible.
6. Keep snapshot projection explicit.

Useful files:

```text
src/Server/Garupan.Server.Match/MatchHost.cs
src/Server/Garupan.Server.Match/MatchHostOptionsFactory.cs
src/Server/Garupan.Server.Match/MatchHostSpawnPlanner.cs
src/Server/Garupan.Server.Match/Outcome/MatchOutcomeTracker.cs
src/Server/Garupan.Server.Console/ServerConsoleOptionsParser.cs
src/Server/Garupan.Server.Console/MatchHostTickLoop.cs
```

## Client Workflow

Use this workflow for client changes:

1. Put shared state and services in `Garupan.Client.Core`.
2. Put screen state, navigation, layout, and draw commands in
   `Garupan.Client.Ui`.
3. Put boot sequencing in `Garupan.Client.Windows.Bootstrap`.
4. Put graphics and platform bindings in `Garupan.Client.Windows.D3D12`.
5. Add pure UI tests before D3D12 tests when possible.

The UI layer should be testable without a graphics device. D3D12 tests should
cover adapter code, host bundle behavior, scene planning, and renderer-specific
paths.

## Content Workflow

Use this workflow for a catalog family:

1. Define a spec type.
2. Add a parser.
3. Add validation.
4. Add tests with small input strings.
5. Wire the spec into server or simulation conversion.
6. Add lint support when cross-file checks matter.

Validation should catch:

- missing required fields;
- malformed numeric values;
- dangling IDs;
- duplicate IDs;
- invalid ranges;
- inconsistent cross-catalog references.

## Tool Workflow

Use this workflow for new commands:

1. Implement `ICommand`.
2. Add an options type if parsing is more than a few flags.
3. Keep scanning and validation code separate from console output.
4. Return a stable exit code.
5. Add command dispatcher tests.
6. Add report tests.

## Network And Protocol Workflow

Session-level frames live in STO. Transport-level behavior lives in Opus.

When adding a frame:

1. Define the frame data.
2. Define the wire shape.
3. Add encoder and decoder tests.
4. Add malformed-input tests.
5. Wire the frame into `ServerSession`, `MatchHost`, or client session code.
6. Keep version handling explicit.

## Snapshot Workflow

When adding state that should replicate:

1. Add the component or state in simulation.
2. Add capture logic.
3. Add encode/decode logic.
4. Add tests for empty, normal, and malformed frames.
5. Update client rendering only after snapshot tests pass.

Keep snapshots compact. Do not add state only because it is convenient to inspect
in a debugger.

## Replay Workflow

Replay code is for deterministic inspection. Add state to replay only if it is
needed to reproduce or diagnose a match.

Recommended checks:

- header round trip;
- frame round trip;
- deterministic scenario replay;
- malformed input handling;
- version mismatch behavior.

## Debugging A Match

1. Reproduce with a low tick rate if possible.
2. Check server logs and parser diagnostics.
3. Confirm the server receives input frames.
4. Inspect snapshots before looking at client rendering.
5. Add a simulation or server test for the smallest failing state.
6. Only then adjust presentation code.

This order avoids chasing a UI symptom when the authoritative state is already
wrong.

## Debugging Client Presentation

1. Confirm the server snapshot contains the expected state.
2. Confirm client session code receives it.
3. Confirm UI state or scene plan changes.
4. Confirm D3D12 host resources exist.
5. Check draw commands or scene draw lists.

Pure UI tests can record draw commands without a GPU. Use those before debugging
a full D3D12 path.

## Debugging Content

1. Run the parser on a minimal input.
2. Check field names and ID casing.
3. Check cross-catalog references.
4. Run content lint.
5. Add a regression test with the smallest row set that reproduces the issue.

Do not fix content problems by hiding defaults in simulation code. Let validation
explain the bad input.

## Common Problems

Troubleshooting and the completion checklist are documented in
[TROUBLESHOOTING.md](TROUBLESHOOTING.md).
