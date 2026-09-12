# Phase 5 — Test Report

Real routing backend (virtual audio endpoint integration). Only executed results
are reported.

## Environment

| Item | Value |
| ---- | ----- |
| OS | Windows 11 Home Single Language, build **26200** |
| .NET | 8.0.425 |
| Host | WSL2 Ubuntu 24.04 executing Windows binaries via interop |

## Build

```
PASS — 0 warnings, 0 errors
```

## Tests

| Category | Result |
| -------- | ------ |
| Unit (Linux) | **93 passed / 0 failed** (12 new routing tests) |
| Windows integration | **15 passed / 0 failed** (3 new routing backend tests) |

New unit tests: virtual device classification heuristics, backend selection
(virtual preferred, policy fallback, none available), virtual pipeline blocked
without a virtual endpoint.

New Windows tests: device enumeration/classification, preferred backend
selection, policy backend targets.

## Physical evidence

| Test | Result | Status |
| ---- | ------ | ------ |
| Virtual endpoint detection | Found `NGENUITY - Chat (HyperX Virtual Audio Device)` | **PASS** (detection) |
| Endpoint-loopback capture | Source peak ~0.34, ~400k frames captured | **PASS** |
| Endpoint renderer (synthetic tone → HyperX) | Target peak **0.5968** | **PASS** (VERIFIED) |
| Endpoint renderer → Speaker/FxSound | Target meter reads 0 in this setup | **PARTIAL** (environment) |
| Real live routing without duplication | No controlled virtual null endpoint | **BLOCKED** |
| Spotify → Speakers (only) | Not executable | **BLOCKED** |
| Chrome → Headphones (only) | Not executable | **BLOCKED** |
| Simultaneous routing | Not executable | **BLOCKED** |
| Close / kill / emergency reset | From Phase 4/4.5 | **PASS** |

## Performance (engine, 1 pipeline, synthetic tone)

| Metric | Value |
| ------ | ----- |
| Working set | ~32–43 MB |
| CPU (cumulative, ~8 s run) | ~1.1 s (~14% of one core) |
| Frames captured | 549,600 |
| Frames rendered | 791,520 |
| Underruns | 0 |
| Latency setting | 100 ms |

(1/3/5 simultaneous pipelines were **not** measured because real routing is
blocked without a virtual endpoint.)

## Verdicts

```
BACKEND ABSTRACTIONS:            PASS
VIRTUAL DEVICE DETECTION:        PASS
ENDPOINT-LOOPBACK CAPTURE:       PASS
ENDPOINT RENDER:                 PASS (physically verified)
REAL LIVE ROUTING (no duplicate): BLOCKED (no virtual endpoint)
POLICY ROUTING:                  PASS (no duplication; applies on stream restart)
PERFORMANCE:                     PARTIAL (1 pipeline measured)
OVERALL:                         PARTIAL
```

## Known limitations

- Live, duplication-free routing requires a virtual audio endpoint that is not
  installed in the test environment; installing third-party software is out of
  scope.
- The detected "virtual" device (HyperX NGENUITY) is not a null sink.
- Some physical endpoints (Speaker Realtek, FxSound) do not register a meter in
  this machine's current configuration.
- No UI peak meter / latency display beyond the backend status card.
- 1/3/5-pipeline performance not measured (blocked).
