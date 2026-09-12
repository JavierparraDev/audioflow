# Session Rules — Audit

Phase 4A. What AudioFlow wrote to Windows before session rules existed, and why
it was a problem.

## Current behavior (before Phase 4)

| Path | What it writes | Survives AudioFlow close? |
| ---- | -------------- | ------------------------- |
| `AudioRoutingManager.Apply` → `AudioPolicyConfig.TrySetPersistedEndpoint` | Per-process **persisted output endpoint** for roles `eConsole` + `eMultimedia` | **Yes** (the problem) |
| `AudioSessionManager.SetProcessMute` | Per-session mute | Transient, but survives until the app restarts if AudioFlow crashes |
| `RuleEngine.SetDefaultDevice` | AudioFlow's own `rules.json` only | Yes, but does not affect Windows |
| `StartupRegistration` | HKCU `Run` value | Yes (a user setting, not audio routing) |

`AudioPolicyConfig` is the internal `IAudioPolicyConfigFactory` used by Windows
Settings. There is **no per-application "clear"**: the only clear call
(`ClearAllPersistedApplicationDefaultEndpoints`, vtable 27) wipes **all** apps'
overrides, so it must not be used for a targeted restore.

## Cleanup gaps found

- The UI's `MainWindow.Closing` called `MainViewModel.Dispose()`, which only
  disposed managers — it never restored the persisted endpoints.
- The console `apply` wrote endpoints and exited, leaving them in place.
- No snapshot, no marker, no crash recovery.

Result: any rule applied by `apply`/the UI could keep affecting Windows after
AudioFlow closed.

## Risks

1. **Persistent state survives process exit and reboot** (the API is "persisted").
2. **PID is not a stable identity** — restore must match by AUMID / path hash /
   executable name, not PID alone.
3. **No targeted clear** — restore must rewrite the original device (or the
   system default) per app, never touch unrelated apps.

## Proposed architecture (implemented in Phase 4B)

```
Start session -> capture originals -> write atomic marker -> apply rules
      ...
End/crash -> restore only the apps AudioFlow touched -> verify -> delete marker
```

- `AudioFlow.Session`: `AudioFlowSessionManager`, `AudioRoutingSnapshot`,
  `SessionMarker`, `AudioRestoreService` (via `IAudioRoutingBackend`),
  `CrashRecoveryService`.
- Snapshot stored in `%APPDATA%\AudioFlow\session\session.json`, written
  **before** any routing change.
- Restore statuses: `Exact`, `Fallback`, `Failed`.
- Only apps recorded in the snapshot are ever modified.
