# Virtual Audio Backend — Research

Phase 4F. How to obtain a virtual audio endpoint so AudioFlow can route per
application **without duplication**.

## Why a virtual endpoint is needed

Phase 3 proved that Process Loopback captures a process's audio but cannot stop
the original output; muting the session also silences the capture. A virtual
endpoint solves this: the application renders to the virtual device (which makes
no physical sound), AudioFlow captures it, and re-renders to the chosen physical
device.

```
App -> Virtual Endpoint -> AudioFlow engine -> Speakers / Headphones
```

## Options

### Option A — Custom AudioFlow virtual driver

Build our own virtual audio device.

- Frameworks: **ACX** (Audio Class eXtension, modern, KMDF-based) or legacy
  **PortCls** + a miniport; AVStream/KS underneath.
- Pros: full control, independent product, monetizable.
- Cons: kernel-mode development, WDK, driver signing (EV certificate + WHQL or
  attestation), Windows version compatibility, security review, long timeline.

### Option B — Microsoft sample-based virtual endpoint

Start from Microsoft's open samples (`Windows-driver-samples` → **Sysvad** /
"Simple Audio Sample", or ACX samples) and adapt them.

- Pros: documented architecture, MIT-licensed sample, realistic starting point.
- Cons: still a driver; still requires signing for distribution; significant
  maintenance.

### Option C — External virtual audio backend (development/validation)

Use an existing virtual audio cable (VB-CABLE, Virtual Audio Cable, VoiceMeeter)
as the capture endpoint during development.

- Pros: no driver work; lets us validate the product and the engine immediately.
- Cons: third-party dependency, redistribution/licensing constraints, install
  friction, not a self-contained product.

### Option D — Hybrid

Use Option C to validate and ship early, migrate to Option B, and only build
Option A if the product justifies it.

## Comparison

| Criterion | A. Custom driver | B. MS sample driver | C. External backend | D. Hybrid |
| --------- | ---------------- | ------------------- | ------------------- | --------- |
| Real routing (no duplication) | Yes | Yes | Yes | Yes |
| Live | Yes | Yes | Yes | Yes |
| Latency | Low | Low | Low | Low |
| Driver required | Yes (own) | Yes (own/adapted) | Yes (third-party) | Yes |
| Admin required | Install yes | Install yes | Install yes | Install yes |
| Complexity | Very high | High | Low | Medium |
| Driver signing | EV + WHQL/attestation | EV + WHQL/attestation | Vendor's | Mixed |
| Windows compatibility | Must maintain | Sample-maintained | Vendor's | Mixed |
| Installation difficulty | High | High | Medium | Medium |
| Monetization | Full | Full | Constrained | Full (later) |
| Security | We own it | We own it | Third-party | Mixed |
| Maintainability | Expensive | Moderate | Vendor's | Mixed |
| Development time | Months | Weeks–months | Days | Staged |
| Production viability | High (long term) | High | Low | High |

## Recommendation

**C → B → A.**

1. **C now:** integrate an optional external virtual endpoint to validate the
   engine and the product (capture from the virtual endpoint, re-render to the
   target, per-app routing by directing apps to the virtual device). This does
   not require a driver and can be documented as a dependency.
2. **B next:** adapt a Microsoft sample to ship a self-contained virtual
   endpoint once the product value is proven.
3. **A last:** only build a fully custom driver if commercial requirements demand
   it.

## What does not change

The `AudioPipeline` / `LiveRoutingManager` built in Phase 3 are reused: only the
capture source changes from process loopback to **endpoint loopback on the
virtual device**. The session/restore safety layer from Phase 4 applies equally.

## Status

Research only. **No driver is implemented in this phase.**
