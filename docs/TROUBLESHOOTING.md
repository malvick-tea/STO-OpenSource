# Troubleshooting And Review

## Common Problems

### Opus project reference cannot be found

Check folder placement. `OpusOpenSource` should be a sibling of
`STO-openSource`, or project references should be updated.

### D3D12 project fails on a non-Windows machine

Build non-D3D12 projects first. D3D12 host and client projects are Windows-only.

### A test fails on missing local runtime content

Run a narrower test project or provide local input for that test. Check whether
the test is intended to cover file-backed behavior.

### Server exits after option parsing

Run with `--help`, then check each flag. The parser rejects invalid ports,
invalid bind addresses, non-positive rates, and a frame pump lower than the tick
rate.

### Tool command finds no keys or catalogs

Check the current shell folder and pass absolute paths. Tool commands do not
guess a workspace root.

## Review Checklist

Before considering a change done:

- The touched project builds.
- Nearby tests pass.
- A second layer test passes when the change crosses a boundary.
- New parser behavior has tests.
- New simulation behavior has deterministic tests.
- New server options have parser tests.
- New UI state has pure UI tests where possible.
- Generated output is not part of the change.
- Local runtime content is not part of the change unless it is a tiny neutral
  fixture.
- Project references still resolve.
