# Device Recovery

How AudioFlow reacts when an output device is disconnected.

## Detection

`AudioDeviceManager` registers `IMMNotificationClient`. The UI forwards these
events to the session layer:

| Notification | Reason prefix |
| ------------ | ------------- |
| `OnDeviceRemoved` | `removed:<deviceId>` |
| `OnDeviceStateChanged` | `state:<deviceId>` |
| `OnDefaultDeviceChanged` | `default:<role>:<deviceId>` |

Only `removed:` and `state:` trigger recovery (a disconnect).

## Recovery flow

```
Device lost (deviceId)
        |
        v
AudioFlowSessionManager.HandleDeviceLost(deviceId)
        |
        +-- find snapshot entries whose target == deviceId
        |
        +-- for each affected application:
        |       restore original device if it still exists  -> Exact
        |       else restore the current Windows default     -> Fallback
        |       if the app is not running or the write fails -> Failed
        |
        +-- persist the session (delete marker if nothing left)
        +-- return DeviceLossReport
```

- **Only affected applications** are restored. Unrelated applications and
  unrelated devices are never touched.
- The original device is used when still available; otherwise the current
  Windows default (`Fallback`).
- The UI logs `DEVICE LOST <app> -> <status>` and updates the session state.

## Statuses

`Exact`, `Fallback`, `Failed` — the same statuses used by restore and shown in
the CLI/UI.

## Fail-safe

If restoring an affected application fails, its entry is kept in the session
snapshot and retried on the next launch or by `audioflow restore`.

## Tests

- Unit: affected-only restore, original-gone fallback, no-affected no-op.
- Windows integration: `HandleDeviceLost` with an unknown device does not throw.

Physical device-disconnect testing requires unplugging/disabling a device and is
marked **NOT TESTED** in [PHASE-4.5-TEST-REPORT.md](PHASE-4.5-TEST-REPORT.md).
