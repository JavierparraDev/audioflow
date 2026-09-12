# Live Routing — Report

Phase 3 (real live audio routing engine). Honest results only.

## BUILD

```
PASS
Warnings: 0
Errors:   0
```

## TESTS

| Category | Result |
| -------- | ------ |
| Unit (Linux) | **64 passed / 0 failed** |
| Windows integration | **9 passed / 0 failed** |
| Physical (capture) | **PASS** |
| Physical (render) | **PASS** |
| Physical (duplication-free routing) | **FAIL** |

## SPOTIFY

| Aspect | Result |
| ------ | ------ |
| Detection | PASS (existing; AUMID stable) |
| Capture | Not driven under controlled playback in this run → **NOT TESTED** (capture engine proven with another process) |
| Render | Not tested for Spotify specifically |
| Speakers | - |
| Headphones | - |
| Live move | Not tested (blocked by duplication) |
| Verification | - |

## CHROME

| Aspect | Result |
| ------ | ------ |
| Detection | PASS (existing) |
| Process model | Chrome uses multiple processes; Process Loopback supports `INCLUDE_TARGET_PROCESS_TREE` |
| Capture | **NOT TESTED** |
| Render | **NOT TESTED** |
| Headphones | **NOT TESTED** |
| Verification | **NOT TESTED** |

## MULTI APPLICATION

```
Spotify -> Speakers
Chrome  -> Headphones
Simultaneous
```

**NOT TESTED** — blocked by the duplication limitation (see below). The
multi-pipeline manager is implemented and unit-tested, but physical simultaneous
routing cannot be honestly claimed without duplication-free capture.

## DUPLICATION

- **Detected?** YES. With capture + re-render, the original endpoint measured
  peak 0.5909 while the target measured 0.2010.
- **Resolved?** NO. The only user-mode suppression (session mute) also silences
  the process loopback capture (peak 0.0000).
- **Architecture:** requires a virtual audio endpoint (see
  [ARCHITECTURE-DECISION-LIVE-ROUTING.md](ARCHITECTURE-DECISION-LIVE-ROUTING.md)).

## PERFORMANCE

Measured on the capture/render pipeline (single application):

| Metric | Value |
| ------ | ----- |
| Capture format | 16-bit PCM / 44100 / stereo |
| Render mix format | 48000 / 2 ch / 32-bit float |
| Renderer latency setting | 100 ms |
| Underruns (8 s run) | 0 |
| Capture frames == render frames | Yes (353,241) |
| CPU / memory | Not benchmarked (not optimized) |

No glitches were observed in the capture/render run (0 underruns).

## KNOWN LIMITATIONS

1. **Duplication** — the original output is not suppressed (session mute kills
   the capture). This is the blocking limitation.
2. Capture format is fixed to 16-bit PCM / 44100 / stereo (same as the Microsoft
   sample); other formats rely on `AUTOCONVERTPCM`.
3. Physical tests for Spotify/Chrome/simultaneous/disconnect were not executed in
   this environment.
4. Process Loopback requires Windows 10 build 20348+ / Windows 11.
5. DRM / protected content is not captured.

## ARCHITECTURE DECISION

```
Process Loopback viable?
PARTIAL — capture and re-render work, but it cannot avoid duplication.
```

A virtual audio endpoint is required for duplication-free live routing.

## FINAL VERDICT

```
NOT ACHIEVED — ARCHITECTURAL LIMITATION
```

The capture → render engine is real, measured and working. Duplication-free live
per-application routing is **not** achievable with Windows Process Loopback alone.
The recommended next architecture is a virtual audio endpoint (driver or virtual
cable), after which the already-built `AudioPipeline`/`LiveRoutingManager` can be
reused with endpoint-loopback capture.

## Evidence index

- [LIVE-ROUTING-TEST-REPORT.md](LIVE-ROUTING-TEST-REPORT.md) — raw physical results
- [ARCHITECTURE-DECISION-LIVE-ROUTING.md](ARCHITECTURE-DECISION-LIVE-ROUTING.md) — decision
- [MANUAL-LIVE-ROUTING-TEST.md](MANUAL-LIVE-ROUTING-TEST.md) — manual verification steps
