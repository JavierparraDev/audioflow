# Phase 4 — Test Report

Session-scoped rules, restore and crash recovery. All results below were
executed on real Windows.

## Environment

| Item | Value |
| ---- | ----- |
| OS | Windows 11 Home Single Language, build **26200** |
| .NET | 8.0.425 |
| Host | WSL2 Ubuntu 24.04 executing Windows binaries via interop |
| Snapshot | `%APPDATA%\AudioFlow\session\session.json` |

## Build

```
PASS — 0 warnings, 0 errors
```

## Tests

| Category | Result |
| -------- | ------ |
| Unit (Linux) | **76 passed / 0 failed** (12 new session tests) |
| Windows integration | **9 passed / 0 failed** |

New session unit tests: marker creation, original capture before apply, exact
restore, fallback restore, idempotent cleanup, identity match across PID change,
unrelated applications untouched, unmute on restore, failed/kept marker when the
app is not running, crash recovery, no-op recovery, stale detection.

## Physical tests

| Test | Expected | Actual | Status |
| ---- | -------- | ------ | ------ |
| `session` initial | INACTIVE | INACTIVE | **PASS** |
| `apply --duration 4` | Temporary session, restored on exit | 3 sessions applied; `Exact` x3; `RESTORE SUCCESS`; INACTIVE after | **PASS** |
| Clean exit restore | Windows normal | Marker deleted, session INACTIVE | **PASS** |
| `restore` with no session | Nothing to do | "No AudioFlow session to restore." / `RESTORE SUCCESS` | **PASS** |
| Crash recovery | ACTIVE -> kill -> STALE -> restore | ACTIVE (owner running) -> `taskkill /F` -> STALE -> `restore` `Exact` x3 `SUCCESS` -> INACTIVE | **PASS** |
| Identity across PID change | Restore by identity | `exe:powershell.exe` restored on a new PID | **PASS** |
| Emergency script | Restore independently | `tools/emergency-restore.ps1` -> `RESTORE SUCCESS` -> INACTIVE | **PASS** |
| UI + tray launch | Window created | `WINDOW=True` | **PASS** (tray icon not visually verified) |
| Unrelated apps preserved | Not touched | Unit-tested; physical not separately measured | **PARTIAL** |
| Windows shutdown/logoff | Restore | Hook present (`Application.SessionEnding`); not executed | **NOT TESTED** |
| Real live routing | - | Not part of this phase | **NOT IMPLEMENTED** |

## Restore statuses observed

- `Exact` — original device restored (all observed cases).
- `Fallback` — only when the original could not be captured (unit-tested).
- `Failed` — app not running; marker kept for retry (unit-tested).

## Known limitations

1. The internal `IAudioPolicyConfigFactory` has **no per-app clear**; restore
   rewrites the original device (or the system default). A behaviourally-neutral
   override entry may remain for apps that had no override.
2. If an app is not running at restore time, restoration is deferred to the next
   launch or `audioflow restore`.
3. After a crash, Windows audio remains changed **until the next AudioFlow
   launch or a manual restore**. The marker makes this recoverable but not
   instantaneous.
4. The tray icon itself was not visually verified; the window launched with the
   tray service active.
5. Windows-shutdown restore is wired but not executed in this environment.

## Verdicts

```
SESSION RULES:                PASS
CLEAN EXIT RESTORE:           PASS
CRASH RECOVERY:               PASS
PHYSICAL WINDOWS RESTORE:     PASS (clean exit + crash recovery)
REAL LIVE ROUTING:            NOT IMPLEMENTED IN THIS PHASE
VIRTUAL AUDIO BACKEND RESEARCH: PASS
OVERALL:                      PASS
```
