# Virtual Endpoint Integration

How AudioFlow uses a virtual audio endpoint to route without duplication.

## Concept

```
Application -> Virtual Endpoint (no physical sound)
                    |
                    v
             AudioFlow endpoint-loopback capture
                    |
                    v
             AudioFlow renderer (WASAPI shared)
                    |
                    v
             Target physical device (Speakers / Headphones)
```

Because the application renders to the virtual endpoint, the original physical
device never plays its audio. This is the only duplication-free live path.

## Detection (no hardcoded names)

`VirtualAudioDeviceManager` enumerates render endpoints and classifies them using
heuristics on the friendly name (`virtual`, `cable`, `vb-audio`, `voicemeeter`,
`loopback`, `audioflow`, ...). It exposes:

- `GetEndpoints()` — all endpoints with `IsVirtual`, `IsDefault`, `State`.
- `GetPhysicalEndpoints()` / `GetVirtualEndpoints()`.
- `GetVirtualRenderEndpoint()` — the first active virtual render endpoint.
- `HasVirtualEndpoint`.

No third-party software is installed and no Windows audio setting is modified
silently.

## Pipeline

`VirtualEndpointRoutingBackend` creates a `VirtualEndpointPipeline`:

1. Resolve the virtual render endpoint (else `Blocked`).
2. `EndpointLoopbackSource.Start(virtualDeviceId)` — capture the virtual
   endpoint's loopback (`WasapiLoopbackCapture`).
3. `WasapiEndpointRenderer.Start(targetDeviceId)` — render to the target, with
   format negotiation/resampling.
4. Wire capture → renderer.
5. Report `PipelineMetrics` (captured/rendered frames, underruns, source peak,
   latency).

## What is verified

- Endpoint-loopback capture works (frames captured, source peak measured).
- The endpoint renderer is **physically verified**: a synthetic tone rendered to
  the HyperX endpoint measured a target peak of **0.5968** (VERIFIED) by
  `AudioOutputVerifier`.

## What is blocked

- **Duplication-free live routing is BLOCKED** in the test environment because
  there is no controlled virtual null endpoint. The only virtual render endpoint
  detected is `NGENUITY - Chat (HyperX Virtual Audio Device)`, which is not a
  null sink (it feeds real hardware), so using it would still duplicate.
- A dedicated virtual endpoint (virtual cable or AudioFlow's own driver) is
  required. See [VIRTUAL-AUDIO-BACKEND-RESEARCH.md](VIRTUAL-AUDIO-BACKEND-RESEARCH.md).

## Safety

The virtual backend is a routing backend; all routing goes through
`AudioFlowSessionManager`, so session snapshot/restore, crash recovery, the
guardian, device-loss handling and Emergency Reset apply equally. Only
applications AudioFlow touched are ever restored.
