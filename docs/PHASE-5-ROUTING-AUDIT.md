# Phase 5 — Routing Audit

Baseline: branch `feature/session-reliability`, build clean, 81 unit tests, 12
Windows integration tests (skip on non-Windows).

## Current architecture

| Component | Project | Responsibility | Reusable? |
| --------- | ------- | -------------- | --------- |
| `ProcessLoopbackCapture` | AudioFlow.ProcessLoopback | Captures one process's audio (official API) | Yes (capture engine) |
| `WasapiProcessRenderer` | AudioFlow.LiveRouting | Renders PCM to a chosen endpoint (shared mode, resampling) | Yes (render engine) |
| `AudioPipeline` | AudioFlow.LiveRouting | Capture → buffer → render, explicit states | Yes |
| `LiveRoutingManager` | AudioFlow.LiveRouting | One pipeline per application | Yes |
| `AudioRoutingManager` | AudioFlow.Core | Persisted endpoint per process (`IAudioPolicyConfigFactory`) | Yes (policy backend) |
| `AudioFlowSessionManager` | AudioFlow.Session | Snapshot, restore, crash recovery, guardian | Yes (safety) |
| `AudioOutputVerifier` | AudioFlow.Core | Measures real endpoint peak | Yes (verification) |

## Why duplication occurs

Process Loopback captures a **copy** of the process's audio but does not stop the
process from rendering to its original endpoint. Measured: target peak 0.2010
**and** original endpoint peak 0.5909 simultaneously.

## Why session mute fails

`ISimpleAudioVolume::SetMute` on the original session also silences the capture
(measured capture peak 0.0000 during mute), because the loopback tap is
downstream of the per-session mute. Muting the original is not a solution.

## What must change

A real, duplication-free backend needs the application to render to a **virtual
endpoint** (which makes no physical sound) that AudioFlow captures:

```
Application
   |
   v
Audio Source Backend   (PolicyEndpoint = persisted endpoint,
   |                     VirtualEndpoint = app renders to a virtual device)
   v
AudioFlow Pipeline     (capture + buffer + format negotiation)
   |
   v
Audio Renderer         (WASAPI shared mode to the target)
   |
   v
Physical Device        (Speakers / Headphones)
```

Two backends:

- **PolicyEndpointRoutingBackend** (available today): sets the persisted
  endpoint. No duplication, no capture — the app renders directly to the target.
  Limitation: applies on stream restart; not live.
- **VirtualEndpointRoutingBackend** (requires a virtual endpoint): the app
  renders to the virtual device; AudioFlow captures the virtual endpoint's
  loopback and renders to the target. Live, no duplication. Blocked when no
  virtual endpoint is installed.

## Reuse plan

- Keep `AudioFlowSessionManager` / guardian / snapshot as the safety layer for
  **both** backends.
- Keep the renderer/buffer concepts; add an endpoint-loopback source.
- The UI depends on the new `AudioFlow.Routing` abstractions, never on Process
  Loopback or a specific virtual device.

## Honest status

No virtual audio endpoint is installed in the test environment, and installing
third-party software is out of scope. The virtual backend is therefore
**BLOCKED** for physical verification; detection and the adapter architecture are
implemented and testable.
