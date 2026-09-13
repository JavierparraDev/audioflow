# Known Limitations

AudioFlow is honest about what it can and cannot do today.

## 1. Routing is not live (the main limitation)

AudioFlow uses `IAudioPolicyConfigFactory` (the same internal interface as the
Windows *App volume and device preferences* page) to set the **persisted output
endpoint** for a process.

- It applies when the application **(re)initializes its audio stream**.
- It does **not** move a currently playing stream.
- In a controlled stream-restart test on Windows 11, the audio did **not** move.
- The interface is **undocumented** and may change between Windows builds.

Result: routing is **PARTIAL**. The official, documented alternative is Process
Loopback (see below).

## 2. Audio Lock is partial protection

Audio Lock redirects the audio that AudioFlow manages. It is **not** a hard
block:

- Applications that select their own endpoint can bypass it.
- Applications using WASAPI **exclusive mode** can bypass it.
- Applications that ignore the persisted policy can bypass it.

## 3. Per-application endpoint choice is inherently per-process

The persisted endpoint is keyed by process id, so it cannot be set before the
process exists. Combined with limitation 1, this makes it unreliable for apps
that keep a long-lived audio stream.

## 4. Application identification can collide

The primary key is `exe:<file name>`. Two different applications with the same
executable name share a key. A `PathHash` (SHA-256 prefix of the path) is stored
and used as a secondary match, but legacy rules may not have it.

## 5. Endpoint metering is a mix, not per-app

`AudioOutputVerifier` measures the **whole endpoint mix**. If another app is
playing on the same device, the measurement is not isolated. Digital silence
(paused track) yields ~0 even when routing is correct.

## 6. Process Loopback is experimental

`AudioFlow.ProcessLoopback` can **activate** the official per-process capture
interface (verified on Windows 11 build 26200). It does **not** yet:

- re-render captured audio to a chosen endpoint;
- mute the original session (audio would be duplicated);
- capture DRM / protected content.

## 7. Other edge cases

- Device removal while routing: the rule is kept; the app falls back when the
  device returns.
- Exclusive-mode games may not be affected.
- `MMDeviceCollection` (NAudio) is not `IDisposable`; its COM object is released
  by the GC.

## 8. Updates and distribution

- The updater applies the **installer** (or a portable ZIP) but cannot replace
  files while AudioFlow is running; it uses a separate process that waits for the
  app to exit.
- Update checks require internet access to GitHub. Offline, AudioFlow works
  normally and simply reports that it could not check.
- Packages are verified by SHA-256. There is **no code signing yet**; signed
  installers are planned.
- A silent uninstall preserves `%APPDATA%\AudioFlow`. Only an interactive
  uninstall asks, and the default is to keep the data.
- Portable mode stores data in `<app>\data`; do not mix a portable install with
  an installed one (different data locations).

## 9. Live routing (Process Loopback) — duplication

Phase 3 implemented and verified a real **Process Loopback capture → WASAPI
render** pipeline (see [LIVE-ROUTING-REPORT.md](LIVE-ROUTING-REPORT.md)).

- Capture works (per process, 265,041 frames / 6 s, peak 0.3554 measured).
- Re-render to a chosen device works (target endpoint peak 0.2010 measured,
  0 underruns, format negotiation 44100/16 → 48000/32).
- **It duplicates audio**: the original output keeps playing. Suppressing it by
  muting the session **also silences the capture** (measured peak 0.0000),
  because the loopback tap is downstream of the per-session mute.
- Therefore duplication-free live routing requires a **virtual audio endpoint**
  (driver or virtual cable). See
  [ARCHITECTURE-DECISION-LIVE-ROUTING.md](ARCHITECTURE-DECISION-LIVE-ROUTING.md).

## 10. Session rules and restore

Routing is now **session-scoped and fully ephemeral**: AudioFlow snapshots the
original per-application audio registry (`PolicyConfig\PropertyStore`), writes an
atomic recovery marker before any change, and restores the exact state on exit or
on the next launch after a crash. Limitations:

- The internal API is **undocumented** and may change between Windows builds.
- Restore rewrites or deletes policy subkeys matched by executable name. Only
  applications AudioFlow recorded are ever touched; unrelated apps are never
  modified.
- If an application is **not running** at restore time, its registry entry is
  still removed, because restore does not depend on the process being alive.
- After a crash, Windows audio stays changed **until the next AudioFlow launch**
  or a manual `audioflow restore` / `tools/emergency-restore.ps1`.
- Restore matches applications by AUMID / path hash / executable name / live PID,
  never by PID alone.
- Files (rules, session marker, logs) are deleted on a clean exit, and
  `audioflow cleanup` / `tools/cleanup.ps1` remove anything left by earlier
  versions.

## 11. Reliability and the session guardian

- **Device disconnect** handling is implemented and unit-tested (affected-only
  restore, exact/fallback), but a physical unplug test was **not executed**.
- The **Session Guardian** restores Windows audio within seconds of a crash
  (verified), but it is a separate process: if the guardian itself is killed
  before restoring, recovery falls back to the next AudioFlow launch.
- **Windows logoff/restart/shutdown** restore is wired via
  `Application.SessionEnding` but was **not physically executed**.
- **Pipeline failure** stops the affected pipeline and the session restores on
  end, but the fail-safe was not triggered physically.
- The guardian restores only applications in the session snapshot; it never
  touches unrelated audio settings.

## 12. Real routing backend (Phase 5)

- **Live, duplication-free routing is BLOCKED** without a virtual audio
  endpoint. The application must render to a virtual endpoint (a null sink) for
  AudioFlow to capture and re-render; no such endpoint is installed in the test
  environment.
- The detected "virtual" device (`NGENUITY - Chat (HyperX Virtual Audio Device)`)
  is **not** a null sink, so using it would still duplicate.
- The **Policy Endpoint** backend works today with no duplication but applies
  when the application recreates its audio stream (not live).
- Endpoint-loopback capture and the endpoint renderer are implemented; the
  renderer is physically verified (tone → target peak 0.5968). Some physical
  endpoints (Speaker Realtek, FxSound) do not register a meter on the test
  machine.
- Physical routing tests for Spotify/Chrome/simultaneous routing were **not
  executed** (blocked).
