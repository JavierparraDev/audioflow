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

Routing is now **session-scoped**: AudioFlow snapshots the original state, writes
an atomic recovery marker before any change, and restores Windows audio on exit
or on the next launch after a crash. Limitations:

- The internal API has **no per-application clear**. Restore rewrites the
  original device (or the system default); a behaviourally-neutral override entry
  may remain for apps that had no override.
- If an application is **not running** at restore time, its restoration is
  deferred to the next launch or to `audioflow restore`.
- After a crash, Windows audio stays changed **until the next AudioFlow launch**
  or a manual `audioflow restore` / `tools/emergency-restore.ps1`.
- Restore matches applications by AUMID / path hash / executable name / live PID,
  never by PID alone.
- Only applications AudioFlow recorded are ever restored; unrelated apps are
  never touched.
