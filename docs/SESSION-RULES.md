# Session Rules

AudioFlow's routing is **session-scoped** and **fully ephemeral**. When AudioFlow
closes, Windows returns to its normal audio behavior and nothing about the
session remains: no rules file, no session file, no logs and no audio registry
change.

```
AudioFlow OPEN   -> rules live in memory and may be active
AudioFlow CLOSED -> Windows audio is normal; no file or registry change remains
AudioFlow CRASH  -> next startup restores Windows audio first
```

## How it works

1. `AudioFlowSessionManager.StartSession()` captures the system default render
   device and a full snapshot of the per-application audio registry
   (`PolicyConfig\PropertyStore`) and writes an atomic recovery marker
   (`session.json`) **before any change**.
2. `ApplyRoute(identity, pid, target)` captures the app's original persisted
   endpoint (once) and persists the snapshot **before** applying the new one.
3. `EndSession()` restores every recorded app, unmutes recorded processes,
   restores the captured registry state exactly (deleting entries AudioFlow
   created), and deletes the marker.

## Ephemeral guarantees

- **No `rules.json`.** Rules live in memory only; the app never writes a rules
  file. Legacy files from earlier versions are deleted on startup and exit.
- **No leftover registry entries.** The `PropertyStore` subtree is restored to
  the exact state captured at session start, even for applications that are no
  longer running.
- **No logs.** Log files are removed on a clean exit.
- **No auto-start.** AudioFlow does not register itself to run at logon.

## Snapshot model

`%APPDATA%\AudioFlow\session\session.json`:

```json
{
  "sessionId": "521809c78a614614b475ce96c695bb2d",
  "createdAt": "2026-09-12T04:58:21Z",
  "state": "active",
  "ownerProcessId": 13968,
  "defaultRenderDeviceId": "{0.0.0...}.{...}",
  "applications": [
    {
      "applicationIdentifier": "exe:spotify.exe",
      "aumid": null,
      "executablePath": "C:\\...\\Spotify.exe",
      "pathHash": "AB12...",
      "executableName": "Spotify.exe",
      "originalDeviceId": "{0.0.0...}.{...}",
      "originalCaptured": true,
      "hadOverride": false,
      "audioFlowTargetDeviceId": "{0.0.0...}.{...}",
      "processIds": [12000]
    }
  ],
  "mutedProcessIds": []
}
```

## Identity priority (restore)

Restoration never relies on PID alone. It matches, in order:

1. AUMID / application identifier
2. Executable full path hash
3. Executable name
4. Any still-running recorded PID

This means a rule survives a PID change (e.g. Spotify restarting).

## Restore statuses

| Status | Meaning |
| ------ | ------- |
| `Exact` | The original device was restored exactly. |
| `Fallback` | The original could not be captured; the current Windows default was used. |
| `Failed` | The app was not running or the write failed; retried on the next launch (marker kept). |

The CLI, UI and logs all distinguish these.

## Safety guarantees

- **Never modifies unrelated applications.** Only apps recorded in the snapshot
  are restored.
- **Marker before change.** Routing is never applied before the recovery marker
  exists.
- **Idempotent cleanup.** `EndSession`, `RestoreAll`, `Emergency Reset` and crash
  recovery can be called repeatedly without corrupting state.
- **Never auto-starts routing after a crash.** Recovery only restores; the user
  must activate routing again.
