# Roadmap

## Done

- [x] Device enumeration (WASAPI / MMDevice).
- [x] Audio session detection with real-time events.
- [x] Stable application identification (name + path hash + AUMID).
- [x] Rule engine with session-only (ephemeral) rules.
- [x] Audio Lock (partial).
- [x] Per-application persisted-endpoint routing.
- [x] Objective routing verification (endpoint peak).
- [x] Premium dark WPF UI with sidebar navigation.
- [x] English + Spanish localization.
- [x] Unit + Windows integration tests, CI.
- [x] Experimental Process Loopback probe.

## Distribution and updates

- [x] Single source of truth for the version (`Directory.Build.props`).
- [x] Self-contained single-file win-x64 release build.
- [x] Inno Setup installer with Start Menu/desktop shortcuts and uninstaller.
- [x] Portable ZIP with isolated configuration.
- [x] Update checker (GitHub Releases, async, offline-safe).
- [x] Standalone updater with SHA-256 verification and configuration backup.
- [x] Developer scripts (`tools/dev-update.ps1`, `install.ps1`, `tools/release.ps1`).
- [x] Release workflow on `v*` tags with checksums and release notes.

## Live routing (Phase 3)

- [x] Real Process Loopback capture with peak/RMS/frame metrics.
- [x] WASAPI renderer with format negotiation (resampling).
- [x] `AudioPipeline` with explicit states and `LiveRoutingManager`.
- [x] Physical verification: capture PASS, render PASS, duplication FAIL.
- [x] Architecture decision: a virtual audio endpoint is required.
- [ ] Virtual audio endpoint (driver or virtual cable) integration.
- [ ] Capture from the virtual endpoint and route to the target device.
- [ ] Spotify/Chrome/simultaneous/disconnect physical tests.

## Session safety (Phase 4)

- [x] Session-scoped rules with an atomic recovery marker.
- [x] Snapshot/restore by stable application identity (not PID alone).
- [x] Restore statuses: Exact / Fallback / Failed.
- [x] Crash recovery on next launch (never auto-restarts routing).
- [x] Clean-exit restore (window, Alt+F4, tray exit, Windows shutdown).
- [x] `audioflow session` / `audioflow restore` / `audioflow cleanup` and
  `tools/emergency-restore.ps1` / `tools/cleanup.ps1`.
- [x] Exact per-application audio registry snapshot/restore (leaves no trace).
- [x] UI session status, safety banner, Emergency Reset and tray.
- [x] Virtual audio backend research (C -> B -> A).
- [ ] Virtual audio endpoint integration (Phase 5).
- [ ] Real live routing (Phase 6).

## Reliability (Phase 4.5)

- [x] Device disconnect recovery (affected-only, exact/fallback).
- [x] Pipeline failure fail-safe (explicit Failed state; restore on end).
- [x] Independent Session Guardian (restores within seconds of a crash).
- [x] Clean-shutdown/guardian race prevention (marker + idempotency).
- [x] `audioflow diagnostics` and `audioflow guardian status`.
- [ ] Physical device-disconnect test.
- [ ] Windows logoff/restart/shutdown physical test.

## Real routing backend (Phase 5)

- [x] Swappable routing abstractions (`AudioFlow.Routing`).
- [x] Virtual audio device detection (heuristics, no hardcoded names).
- [x] Policy Endpoint backend (no duplication; stream-restart).
- [x] Virtual Endpoint backend (endpoint-loopback capture → render).
- [x] Endpoint renderer physically verified (tone → peak 0.5968).
- [x] CLI `routing` / `routing-test`; UI backend status card.
- [ ] Install/obtain a virtual audio endpoint (virtual cable or own driver).
- [ ] Physical tests 1–4 (Spotify/Chrome/simultaneous) — BLOCKED without it.
- [ ] 1/3/5-pipeline performance measurement.

## Next

- [ ] **Live routing via Process Loopback**: capture a process and re-render to
  the target endpoint, muting the original session.
- [ ] Verify live routing with `AudioOutputVerifier` per app.
- [ ] Profiles / scenes (Game mode, Streaming mode).
- [ ] Per-app volume and mute in the UI.
- [ ] Device reconnection handling with stable identifiers (Container ID).
- [ ] Additional languages.
- [ ] Code signing for installers and binaries.
- [ ] Optional signed virtual audio driver for perfect routing.
