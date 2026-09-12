# Routing Backends

AudioFlow routes through a swappable backend layer (`src/AudioFlow.Routing`).
The UI and the session safety layer depend only on the abstractions, never on
Process Loopback or a specific virtual audio implementation.

## Abstractions

- `IRoutingBackend` — `Info`, `GetTargets()`, `CreatePipeline(...)`.
- `IRoutingPipeline` — `State`, `Metrics`, `Start/Stop/MoveTo`, `StateChanged`.
- `AudioEndpointInfo` — `DeviceId`, `FriendlyName`, `DeviceType`, `State`,
  `IsVirtual`, `IsDefault`, `IsAvailable`.
- `RoutingBackendInfo` — `Name`, `Kind`, `IsAvailable`, `Reason`, `Status`.
- `PipelineState` — `Created/Starting/Running/Stopping/Stopped/Failed/Blocked`.
- `PipelineMetrics` — captured/rendered frames, underruns, source peak, latency.

## Backends

| Backend | Kind | Live | Duplication | Available |
| ------- | ---- | ---- | ----------- | --------- |
| `PolicyEndpointRoutingBackend` | PolicyEndpoint | No (stream restart) | None | Always (Windows) |
| `VirtualEndpointRoutingBackend` | VirtualEndpoint | Yes | None (when a virtual endpoint exists) | Only if a virtual endpoint is detected |

### Policy Endpoint

Sets the application's persisted output endpoint (the mechanism used by Windows
Settings). No capture, so no duplication; the application renders directly to
the target. Limitation: applies when the application recreates its audio stream.

### Virtual Endpoint

The application renders to a virtual endpoint (which makes no physical sound);
AudioFlow captures that endpoint's **loopback** and renders it to the chosen
physical target. Live and duplication-free. Requires a virtual audio endpoint to
be installed; otherwise the backend reports `BLOCKED`.

## Selection

`RoutingBackendRegistry.SelectPreferred()` prefers the Virtual Endpoint backend
when available, otherwise the Policy Endpoint backend.

```
AudioFlow UI / CLI
        |
        v
RoutingBackendRegistry.SelectPreferred()
        |
   +----+-----------------------------+
   |                                  |
Virtual Endpoint (if available)   Policy Endpoint (fallback)
```

## Status values

- `AVAILABLE` — the backend can create a running pipeline.
- `BLOCKED` — a prerequisite is missing (e.g. no virtual endpoint).

The UI and CLI display these; nothing claims live routing unless the virtual
backend is actually available.
