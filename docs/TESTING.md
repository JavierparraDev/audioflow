# Testing

AudioFlow has two test categories.

## Unit tests (any OS)

Project: `tests/AudioFlow.Tests` (xUnit, targets `net8.0-windows` but only exercises
pure logic, so it runs on Linux/macOS/Windows).

```bash
dotnet test tests/AudioFlow.Tests -c Release
```

Coverage:

- **AudioRule** - creation, defaults, ids.
- **RuleEngine** - Spotify -> Speakers, unknown -> default, Discord/Chrome ->
  Headphones, disabled rule -> default, multiple rules, PathHash secondary match,
  Audio Lock allow/block, remove rule.
- **RuleStorage** - save/load round-trip, missing file, empty file, invalid JSON,
  corrupt recovery, atomic save (no temp left), directory creation.
- **ApplicationIdentifier** - same executable across PIDs, different executables,
  path hash stability, unknown application, case-insensitive names, Win32 kind.

## Windows integration tests (Windows only)

Project: `tests/AudioFlow.Windows.Tests`.

Tests are marked with a custom `[WindowsFact]` attribute that sets `Skip` when the
host is not Windows, so the build and test run never fail on Linux/macOS.

```powershell
dotnet test tests/AudioFlow.Windows.Tests -c Release
```

Coverage:

- Output device enumeration (ids, names, state).
- Default endpoint.
- Active audio session enumeration (PID, state, volume, peak, device).
- Session monitor start/stop.
- Application identity for the current process.
- `AudioOutputVerifier` endpoint peak measurement.

## Routing verification (manual / semi-automated)

Because routing depends on physical audio, it is verified with the endpoint meter
rather than an API return value:

```powershell
audioflow verify "Speakers"
```

This measures the real audio level of every endpoint for 3 seconds. See
[TEST-REPORT.md](TEST-REPORT.md) for actual results and [LIMITATIONS.md](LIMITATIONS.md)
for why an API success is not proof of physical routing.

## Continuous integration

`.github/workflows/build.yml`:

- **Linux job**: restore, build, unit tests, Windows tests (skipped), upload `.trx`.
- **Windows job**: restore, build, unit tests, Windows integration tests, upload `.trx`.
