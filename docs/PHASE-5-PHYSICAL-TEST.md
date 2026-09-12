# Phase 5 — Physical Test Plan

Reproducible steps and current status. Do not mark PASS without real audio
measurement.

## Tools

- `audioflow routing` — backend + virtual endpoint status.
- `audioflow routing-test <source> <target> <seconds> [--tone]` — endpoint
  loopback capture → render, with real endpoint peaks.
- `audioflow verify <device>` — measures the real level of every endpoint.
- `audioflow diagnostics` — session/guardian/backend/devices/routes.

## Status key

`PASS` · `PARTIAL` · `FAIL` · `NOT TESTED` · `BLOCKED`

## Tests

| # | Test | How | Expected | Status |
| - | ---- | --- | -------- | ------ |
| 1 | Spotify → Speakers | Route Spotify to Speakers; `verify` Speakers + Headphones | Speakers peak > threshold, Headphones ≈ 0, no duplicate | **BLOCKED** (no virtual endpoint for live; policy applies on stream restart) |
| 2 | Chrome YouTube → Headphones | Same with Chrome → Headphones | Headphones > 0, Speakers ≈ 0 | **BLOCKED** |
| 3 | Spotify + Chrome | Two independent targets | Each app only on its target | **BLOCKED** |
| 4 | Spotify + Chrome + Discord | Three targets | Three independent paths | **BLOCKED** |
| 5 | Close AudioFlow | Session rules + close | Windows audio restored | **PASS** (Phase 4/4.5) |
| 6 | Kill AudioFlow | `taskkill /F` + guardian | Guardian restores | **PASS** (Phase 4.5) |
| 7 | Disconnect Headphones | Unplug during routing | Device loss handled, app restored, fallback only if needed | **NOT TESTED** (logic unit-tested) |
| 8 | Reconnect Headphones | Replug | Detected; no unsafe auto-routing | **NOT TESTED** |
| 9 | Emergency Reset | UI button / `audioflow restore` | Only AudioFlow-touched apps restored | **PASS** (Phase 4.5) |

## Engine verification (executed)

These verify the capture/render engine, not duplication-free routing:

- `audioflow routing-test "Auriculares (HyperX" "Auriculares (HyperX" 3 --tone`
  → target peak **0.5968**, `VERIFIED` (renderer physically confirmed).
- `audioflow routing-test "Auriculares (HyperX" "Speaker (Realtek" 6`
  → source captured (peak ~0.34, ~400k frames), but the Realtek endpoint meter
  reads 0 in this machine's current configuration (endpoint not registering).

## Why tests 1–4 are BLOCKED

Live, duplication-free routing requires the application to render to a **virtual
null endpoint**. No such endpoint is installed in the test environment (and
installing third-party software is out of scope). The detected virtual device
(HyperX NGENUITY) is not a null sink.

## To run tests 1–4 later

1. Install a virtual audio endpoint (e.g. a virtual cable) or AudioFlow's own
   endpoint.
2. `audioflow routing` → confirm the Virtual Endpoint backend is `AVAILABLE`.
3. Direct each application to the virtual endpoint (policy or default).
4. `audioflow routing-test <virtualEndpoint> <physicalTarget> <seconds>` and
   `audioflow verify <target>`; confirm the non-target endpoint is silent.
