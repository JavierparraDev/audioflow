# Crash Recovery

AudioFlow must always be able to return Windows to its normal audio behavior,
even after a crash, a `kill`, or a power loss.

## Session marker

`%APPDATA%\AudioFlow\session\session.json` is written **atomically** (temp file +
replace) and records the session snapshot, including the owning PID.

## Recovery triggers

| Situation | Mechanism |
| --------- | --------- |
| Window close / Alt+F4 | `MainWindow.Closing` → stop session → restore → exit |
| Tray "Exit" | same |
| Windows shutdown/logoff | `Application.SessionEnding` → restore |
| Console `Ctrl+C` / exit | `apply` restores before returning |
| Unhandled exception | UI logs and `Dispose` restores |
| `taskkill /F` / crash / power loss | marker survives → next launch recovers |
| Manual | `audioflow restore` or `tools/emergency-restore.ps1` |

## Recovery flow on startup

1. Detect `session.json`.
2. Read the snapshot.
3. Restore every recorded application (identity-matched, not PID-only).
4. Restore the captured audio registry state exactly (deleting entries AudioFlow
   created), so applications that are no longer running are cleaned too.
5. Verify by reading the persisted endpoint back.
6. Delete the marker once the registry state is clean.
7. **Do not re-activate routing** — the user must enable it again.

Because the registry restore does not depend on the application being alive, a
crashed session leaves no audio change behind even if the app was closed before
AudioFlow exited.

## CLI

```
audioflow session   -> ACTIVE | INACTIVE | STALE
audioflow restore   -> RESTORE SUCCESS | RESTORE FAILED
```

`session` reports `STALE` when a marker exists but its owning PID is no longer
running.

## Emergency script

`tools/emergency-restore.ps1` works independently of the UI/pipeline. It locates
the AudioFlow CLI and runs `restore`. It never resets unrelated applications and
never calls the destructive "clear all endpoints" API.

## Guarantees

- Idempotent: repeated restores are safe.
- Targeted: only applications AudioFlow modified.
- Fail-safe: if a restore fails, the marker is kept for retry; Windows is never
  left with a half-applied session silently.
