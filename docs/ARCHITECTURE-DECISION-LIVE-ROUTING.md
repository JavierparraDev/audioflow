# Architecture Decision — Live Audio Routing

**Status:** Decided (Phase 3)
**Date:** 2026-09-11
**Decision:** Process Loopback capture + WASAPI re-render is implemented and
verified, but it **cannot** deliver duplication-free live routing on its own.
A **virtual audio endpoint** is required for a complete product.

---

## 1. What was attempted

A pipeline per application:

```
Application (process)
        │  Windows Process Loopback (AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS)
        ▼
  ProcessLoopbackCapture  (16-bit PCM / 44100 / stereo)
        │  BufferedWaveProvider + resampler
        ▼
  WasapiProcessRenderer   (device mix format, shared mode)
        ▼
  Selected output device
```

Both halves were implemented and verified on Windows 11 build 26200:

- Capture: 265,041 frames in 6 s, peak 0.3554 (see
  [LIVE-ROUTING-TEST-REPORT.md](LIVE-ROUTING-TEST-REPORT.md)).
- Render: capture frames == render frames (353,241), 0 underruns, and the target
  endpoint measured **peak 0.2010** while the pipeline ran.

So capture and re-render **work**.

## 2. The blocker: duplication

Process Loopback captures a copy of the process's audio; it does **not** stop the
process from rendering to its original device. Without suppression:

```
Speaker  peak = 0.2010   (our re-render)
HyperX   peak = 0.5909   (original output, duplicated)
```

The obvious suppression is muting the original session
(`ISimpleAudioVolume::SetMute`). We tested it:

```
mute ON  -> capture peak = 0.0000 for the whole mute window
mute OFF -> capture peak resumes
```

**Conclusion:** the process loopback tap is *downstream* of the per-session
mute, so muting the original also silences the capture. Suppression via session
mute is therefore impossible.

This was verified twice (standalone capture test and `live-route --mute`).

## 3. Alternatives evaluated

| Architecture | Real routing | Live | Latency | Driver | Complexity | Commercial |
| ------------ | ------------ | ---- | ------- | ------ | ---------- | ---------- |
| A. Windows Audio Policy (`IAudioPolicyConfigFactory`) | Partial | No (stream restart) | N/A | No | Low | OK but not live; already implemented (0.2.x) |
| B. Process Loopback + re-render | Yes, but **duplicated** | Yes | Low (100 ms) | No | Medium | Not acceptable (double audio) |
| C. Process Loopback + session mute | **No** (mute kills capture) | - | - | No | Low | Not viable |
| D. Virtual audio endpoint (driver or virtual cable) | **Yes, no duplication** | Yes | Low | Yes* | High | **Required for the product** |
| E. Hybrid: policy for restartable apps + virtual endpoint for live | Yes | Mostly | Low | Yes* | High | Best long-term |

\* A virtual endpoint can be a custom signed driver, or an existing virtual
cable (VB-CABLE, Virtual Audio Cable, VoiceMeeter). A custom driver requires
driver signing (EV/WHQL).

## 4. Why session-mute cannot be fixed in user mode

The Windows audio graph is roughly:

```
app stream -> session gain/mute -> process loopback tap? -> endpoint mix -> endpoint volume -> hardware
```

The experiment shows the tap is **after** session gain/mute. Any per-session
volume change used to silence the original also silences the tap. Muting the
*endpoint* is the only downstream stage, but it mutes **all** applications on
that endpoint, so it is not a general solution.

## 5. Decision

1. Keep Process Loopback capture + WASAPI render as the **capture/render
   engine**. It is verified and reusable.
2. Do **not** ship a "live routing" feature that duplicates audio, and do not
   pretend session mute solves it.
3. The product path is a **virtual audio endpoint**:
   - the application renders to the virtual endpoint (via policy or by being the
     default);
   - AudioFlow captures the virtual endpoint (endpoint loopback is enough) and
     re-renders to the chosen physical device;
   - the original output is the virtual endpoint, which produces no physical
     sound → no duplication.
4. Reuse the implemented `AudioPipeline`/`LiveRoutingManager`; only the capture
   source changes (endpoint loopback on the virtual device instead of process
   loopback).

## 6. Next steps

- [ ] Choose virtual endpoint strategy: integrate an existing virtual cable vs.
  build/sign a driver.
- [ ] Capture from the virtual endpoint and route to the target physical device.
- [ ] Per-app routing by directing each app to a virtual endpoint via policy.
- [ ] Re-run the physical verification matrix
  ([MANUAL-LIVE-ROUTING-TEST.md](MANUAL-LIVE-ROUTING-TEST.md)).
- [ ] Re-evaluate latency and CPU with the virtual endpoint in the graph.

## 7. Verdict

```
FINAL VERDICT: NOT ACHIEVED — ARCHITECTURAL LIMITATION
```

Process Loopback capture + WASAPI render is **proven and working**, but
duplication-free live routing is **not achievable with Process Loopback alone**.
A virtual audio endpoint is required.
