# Phase 4.5 — Test Report

Reliability, device safety and the session guardian. Only executed results are
reported.

## Environment

| Item | Value |
| ---- | ----- |
| OS | Windows 11 Home Single Language, build **26200** |
| .NET | 8.0.425 |
| Host | WSL2 Ubuntu 24.04 executing Windows binaries via interop |

## Build

```
PASS — 0 warnings, 0 errors
```

## Tests

| Category | Result |
| -------- | ------ |
| Unit (Linux) | **81 passed / 0 failed** (5 new device-loss/restore tests) |
| Windows integration | **12 passed / 0 failed** (3 new session tests) |

## Physical tests

| # | Test | Expected | Actual | Status |
| - | ---- | -------- | ------ | ------ |
| 1 | Clean exit (`apply` ends) | Restore + marker removed | `Exact` x3, `RESTORE SUCCESS`, INACTIVE | **PASS** |
| 2 | Alt+F4 | Restore on close | Code path present; not physically triggered | **NOT TESTED** |
| 3 | Tray exit | Restore on exit | Code path present; not physically triggered | **NOT TESTED** |
| 4 | `taskkill /F` + next launch | Restore on startup | ACTIVE -> STALE -> `restore` Exact x3 -> INACTIVE | **PASS** |
| 4b | `taskkill /F` + **guardian** (no restart) | Guardian restores | Owner killed -> guardian restored `exact=2 fallback=0 failed=0` in ~5 s, report written, guardian exited | **PASS** |
| 4c | Clean shutdown vs guardian | Guardian must not restore | Marker removed -> guardian logged "clean shutdown" and exited; no recovery report | **PASS** |
| 5 | Device disconnect | Restore affected only | Logic unit-tested; physical disconnect not executed | **NOT TESTED** |
| 6 | Device reconnect | Re-route on return | Not executed | **NOT TESTED** |
| 7 | Windows logoff | Restore | Hook present; not executed | **NOT TESTED** |
| 8 | Windows restart | Restore | Not executed | **NOT TESTED** |
| 9 | Windows shutdown | Restore | Hook present; not executed | **NOT TESTED** |
| 10 | Pipeline failure | Stop route + restore | Pipeline has explicit `Failed` state; session restores on end; not physically triggered | **PARTIAL** |

### Guardian crash evidence

`%APPDATA%\AudioFlow\session\guardian-report.json`:

```json
{
  "Timestamp": "2026-09-12T05:19:35+00:00",
  "HadStaleSession": true,
  "Success": true,
  "Summary": "exact=2 fallback=0 failed=0",
  "Applications": [
    { "ApplicationIdentifier": "exe:powershell.exe", "Status": "Exact" },
    { "ApplicationIdentifier": "exe:chrome.exe", "Status": "Exact" }
  ]
}
```

Guardian log:

```
[00:20:14] Guardian started.
[00:20:21] Session marker removed (clean shutdown). Guardian exiting.
```

## Device disconnect

The disconnect path (`AudioDeviceManager` notifications -> session
`HandleDeviceLost`) is implemented and unit-tested (affected-only, exact,
fallback, no-op). A **physical** device unplug was not executed in this
environment, so it is marked `NOT TESTED` rather than PASS.

## Verdicts

```
DEVICE DISCONNECT SAFETY:        PARTIAL  (logic + tests; physical not executed)
PIPELINE FAILURE RECOVERY:       PARTIAL  (Failed state + restore-on-end; not physically triggered)
SESSION GUARDIAN:                PASS     (crash restore + clean-shutdown coordination)
CLEAN EXIT:                      PASS
CRASH RECOVERY WITHOUT RESTART:  PASS     (guardian)
WINDOWS SHUTDOWN:                NOT TESTED
OVERALL:                         PARTIAL
```
