# Roadmap

## Done

- [x] Device enumeration (WASAPI / MMDevice).
- [x] Audio session detection with real-time events.
- [x] Stable application identification (name + path hash + AUMID).
- [x] Rule engine + JSON persistence (atomic save).
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

## Next

- [ ] **Live routing via Process Loopback**: capture a process and re-render to
  the target endpoint, muting the original session.
- [ ] Verify live routing with `AudioOutputVerifier` per app.
- [ ] Profiles / scenes (Game mode, Streaming mode).
- [ ] Per-app volume and mute in the UI.
- [ ] Device reconnection handling with stable identifiers (Container ID).
- [ ] Tray icon and start-with-Windows.
- [ ] Additional languages.
- [ ] Optional signed virtual audio driver for perfect routing.
