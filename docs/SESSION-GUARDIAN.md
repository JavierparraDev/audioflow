# Session Guardian

An independent process that restores Windows audio if AudioFlow crashes **before
the next launch**.

```
AudioFlow UI
   |  session id + owner pid
   v
AudioFlow.SessionGuardian  (separate process)
   |
   +-- monitors the owner process
   +-- monitors the session marker
   +-- emergency restore on owner death
```

## Why a separate process

The AudioFlow process cannot restore anything after it has crashed. The guardian
is a small, independent console process that observes the owner and the session
marker, and performs the restore using the same session snapshot.

## Lifecycle

```
Start session
  -> marker written (state active, ownerProcessId)
  -> guardian launched with --owner-pid <pid>

Normal case
  -> guardian polls: owner alive, marker present -> no action

Clean exit
  -> owner restores, deletes marker
  -> guardian sees the marker gone -> logs "clean shutdown" -> exits
     (no restore, no report)

Crash
  -> owner dies, marker still present
  -> guardian detects owner death
  -> AudioFlowSessionManager.RecoverIfNeeded()
  -> verify, delete marker if fully restored
  -> write guardian-report.json
  -> exit
```

## Race prevention

- The owner **deletes the marker** only after a successful restore.
- The guardian acts only when the owner is **not running** and the marker still
  exists.
- Both restore paths are **idempotent**, so even a rare overlap is safe.
- The marker records the owner PID and a lifecycle `state`
  (`active` -> `restoring` -> deleted).

## Command line

```
AudioFlow.SessionGuardian --owner-pid <pid> [--poll <ms>] [--once] [--marker <path>]
```

- `--once` performs a single recovery pass and exits (used for testing).
- `--marker` overrides the marker path.

## Output

- `%APPDATA%\AudioFlow\logs\guardian.log`
- `%APPDATA%\AudioFlow\session\guardian-report.json` (only when it restores)

## Guarantees

- Never restores unrelated settings: only applications in the snapshot.
- Never restarts routing: recovery restores Windows audio only.
- Idempotent and safe to run concurrently with a clean shutdown.

## Status

`audioflow guardian status` -> `RUNNING` / `NOT RUNNING`.
